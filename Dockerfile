FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble

WORKDIR /usr/src/app

# Bundle App Source
COPY Credfeto.Dispatcher.Server .
COPY appsettings.json .
COPY healthcheck .

# hadolint ignore=DL3008
RUN apt-get update && apt-get upgrade -y && apt-get install curl -y --no-install-recommends && apt-get autoremove -y && apt-get clean && rm -rf /var/lib/apt/lists/*

# Smoke test: verify the trimmed binary starts and /priorities returns HTTP 200.
# Catches trim-related runtime failures (missing migrations, stripped types) at build time.
# hadolint ignore=DL4006
RUN Discord__WebhookUrl=http://localhost:1/dummy \
    Discord__NotificationsChannelWebhookUrl=http://localhost:1/dummy \
    GitHub__Token=smoke-test-dummy \
    Database__ConnectionString='' \
    sh -c 'set -e; \
    ./Credfeto.Dispatcher.Server >/tmp/smoke.log 2>&1 & PID=$!; \
    i=0; while [ $i -lt 30 ]; do \
        if curl --noproxy localhost -sf http://localhost:8080/priorities >/dev/null 2>&1; then \
            STATUS=$(curl --noproxy localhost -s -o /dev/null -w "%{http_code}" http://localhost:8080/priorities); \
            kill $PID; rm -rf data/; \
            [ "$STATUS" = "200" ] && exit 0 || { echo "Bad HTTP status: $STATUS"; exit 1; }; \
        fi; \
        i=$((i+1)); sleep 1; \
    done; \
    kill $PID; echo "SMOKE TEST FAILED: port 8080 never opened"; cat /tmp/smoke.log; exit 1'

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT [ "/usr/src/app/Credfeto.Dispatcher.Server" ]

# Perform a healthcheck.  note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/healthcheck" ]
