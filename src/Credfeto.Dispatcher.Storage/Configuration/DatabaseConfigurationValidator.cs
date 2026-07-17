using Microsoft.Extensions.Options;

namespace Credfeto.Dispatcher.Storage.Configuration;

public sealed class DatabaseConfigurationValidator : IValidateOptions<DatabaseConfiguration>
{
    public ValidateOptionsResult Validate(string? name, DatabaseConfiguration options)
    {
        if (options.Provider == DatabaseProvider.SqlServer && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Database ConnectionString must be configured when Provider is SqlServer.");
        }

        return ValidateOptionsResult.Success;
    }
}
