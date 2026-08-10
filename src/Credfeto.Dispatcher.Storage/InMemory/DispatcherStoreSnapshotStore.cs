using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dispatcher.Storage.Configuration;
using Credfeto.Dispatcher.Storage.InMemory.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Storage.InMemory;

// Handles the on-disk mechanics of a snapshot: atomic writes (temp file + rename, so a
// crash/kill mid-write can't corrupt the target) and tolerant reads (missing/corrupt file ->
// log and report "nothing to load", never throw). Holds no in-memory state of its own.
public sealed class DispatcherStoreSnapshotStore
{
    private readonly string _directoryPath;
    private readonly string _filePath;
    private readonly ILogger<DispatcherStoreSnapshotStore> _logger;

    public DispatcherStoreSnapshotStore(IOptions<SnapshotOptions> options, ILogger<DispatcherStoreSnapshotStore> logger)
    {
        this._logger = logger;

        SnapshotOptions value = options.Value;
        this._directoryPath = ResolveDirectoryPath(value.DirectoryPath);
        this._filePath = Path.Combine(this._directoryPath, value.FileName);
    }

    // Blank DirectoryPath falls back to <current working directory>/data (e.g.
    // /usr/src/app/data given the container's WorkingDirectory), matching the already-mounted
    // "dispatcher-data" Docker volume without hard-coding a container-only path here.
    private static string ResolveDirectoryPath(string configured)
    {
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "data")
            : configured;
    }

    // Returns whether the save actually completed, so a caller (SnapshotWriterService) can tell
    // a genuine write from a failed one and knows not to treat this version as durably saved -
    // otherwise a persistent write failure (unmounted volume, read-only mount, full disk) would
    // be recorded as success on the first attempt and never retried.
    internal async ValueTask<bool> SaveAsync(DispatcherStoreSnapshotData data, CancellationToken cancellationToken)
    {
        string? tempPath = null;

        try
        {
            Directory.CreateDirectory(this._directoryPath);

            tempPath = Path.Combine(this._directoryPath, Path.GetRandomFileName());

            await using (FileStream stream = new(path: tempPath, mode: FileMode.Create, access: FileAccess.Write))
            {
                await JsonSerializer.SerializeAsync(
                    utf8Json: stream,
                    value: data,
                    jsonTypeInfo: DispatcherStoreSnapshotSerializerContext.Default.DispatcherStoreSnapshotData,
                    cancellationToken: cancellationToken
                );
            }

            File.Move(sourceFileName: tempPath, destFileName: this._filePath, overwrite: true);
            tempPath = null;

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this._logger.SnapshotSaveFailed(filePath: this._filePath, exception: exception);

            return false;
        }
        finally
        {
            if (tempPath is not null)
            {
                TryDeleteTempFile(tempPath: tempPath, logger: this._logger);
            }
        }
    }

    // Deliberately synchronous: the caller (DispatcherStoreSnapshotLoader, via ServerStartup)
    // must complete this before the host starts accepting requests or the scanner's first tick
    // fires - see the ordering note in ServerStartup.CreateApp.
    [SuppressMessage(
        "Meziantou.Analyzer",
        "MA0045:Do not use blocking calls in a sync method (need to make calling method async)",
        Justification = "Deliberately synchronous - must complete before the host starts accepting requests, see the ordering note in ServerStartup.CreateApp"
    )]
    internal bool TryLoad([NotNullWhen(true)] out DispatcherStoreSnapshotData? data)
    {
        data = null;

        if (!File.Exists(this._filePath))
        {
            return false;
        }

        try
        {
            using FileStream stream = new(path: this._filePath, mode: FileMode.Open, access: FileAccess.Read);

            DispatcherStoreSnapshotData? deserialized = JsonSerializer.Deserialize(
                utf8Json: stream,
                jsonTypeInfo: DispatcherStoreSnapshotSerializerContext.Default.DispatcherStoreSnapshotData
            );

            // System.Text.Json does not enforce non-nullable reference types at runtime: a
            // structurally valid document missing one of the four array properties (e.g. "{}",
            // or a file written by some future/older schema) deserializes successfully with
            // those properties left null, rather than throwing. Treat that the same as a corrupt
            // file - log and report "nothing to load" - instead of handing InMemoryDispatcherStore
            // a snapshot whose ImportSnapshot would NullReferenceException on the first foreach.
            if (
                deserialized
                is not { Repos: not null, PullRequests: not null, Issues: not null, PollingStates: not null }
            )
            {
                this._logger.SnapshotLoadIncomplete(filePath: this._filePath);

                return false;
            }

            data = deserialized;

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            this._logger.SnapshotLoadFailed(filePath: this._filePath, exception: exception);

            return false;
        }
    }

    private static void TryDeleteTempFile(string tempPath, ILogger logger)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.SnapshotTempFileCleanupFailed(filePath: tempPath, exception: exception);
        }
    }
}
