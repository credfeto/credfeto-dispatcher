FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble

WORKDIR /usr/src/app

# Bundle App Source
COPY Credfeto.Dispatcher.Server .
COPY appsettings.json .
COPY healthcheck .

# hadolint ignore=DL3008
RUN apt-get update && apt-get upgrade -y && apt-get install curl -y --no-install-recommends && apt-get autoremove -y && apt-get clean && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT [ "/usr/src/app/Credfeto.Dispatcher.Server" ]

# Perform a healthcheck.  note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/healthcheck" ]
