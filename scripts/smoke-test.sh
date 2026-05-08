#!/usr/bin/env bash
# Smoke test for the trimmed self-contained Credfeto.Dispatcher.Server binary.
# Publishes for the current platform, starts the server with dummy config,
# hits /priorities, asserts HTTP 200 with empty-array body, then cleans up.
# Exit 0 = pass; exit 1 = fail.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="$(mktemp -d)"
SERVER_PID=""

cleanup() {
    if [ -n "$SERVER_PID" ] && kill -0 "$SERVER_PID" 2>/dev/null; then
        kill "$SERVER_PID" 2>/dev/null || true
        wait "$SERVER_PID" 2>/dev/null || true
    fi
    rm -rf "$PUBLISH_DIR"
}
trap cleanup EXIT

RID="${1:-linux-x64}"

echo "Publishing trimmed self-contained binary (RID=$RID)..."
TMPDIR="${TMPDIR:-/tmp}" dotnet publish \
    "$REPO_ROOT/src/Credfeto.Dispatcher.Server/Credfeto.Dispatcher.Server.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained \
    -o "$PUBLISH_DIR" \
    --nologo \
    -v quiet

echo "Starting server..."
export Discord__WebhookUrl=http://localhost:1/dummy
export Discord__NotificationsChannelWebhookUrl=http://localhost:1/dummy
export GitHub__Token=smoke-test-dummy
export ASPNETCORE_ENVIRONMENT=Development

"$PUBLISH_DIR/Credfeto.Dispatcher.Server" > /tmp/smoke-server.log 2>&1 &
SERVER_PID=$!

echo "Waiting for port 8080 (up to 30s)..."
READY=0
for i in $(seq 1 30); do
    if bash -c "echo > /dev/tcp/localhost/8080" 2>/dev/null; then
        echo "  Port open after ${i}s"
        READY=1
        break
    fi
    if ! kill -0 "$SERVER_PID" 2>/dev/null; then
        echo "ERROR: Server exited prematurely after ${i}s"
        cat /tmp/smoke-server.log
        exit 1
    fi
    sleep 1
done

if [ "$READY" -eq 0 ]; then
    echo "ERROR: Port 8080 did not open within 30s"
    cat /tmp/smoke-server.log
    exit 1
fi

echo "Testing GET /priorities..."
STATUS=$(curl --noproxy localhost -s -o /tmp/smoke-response.json -w "%{http_code}" http://localhost:8080/priorities)

if [ "$STATUS" != "200" ]; then
    echo "ERROR: /priorities returned HTTP $STATUS (expected 200)"
    echo "Response body: $(cat /tmp/smoke-response.json)"
    echo "Server log:"
    cat /tmp/smoke-server.log
    exit 1
fi

BODY=$(cat /tmp/smoke-response.json)
if [ "$BODY" != "[]" ]; then
    echo "ERROR: /priorities returned unexpected body: $BODY"
    exit 1
fi

echo "SMOKE TEST PASSED: /priorities returned HTTP 200 []"
