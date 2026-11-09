FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-azurelinux3.0-distroless-extra@sha256:fbe96e7488473f3de62559411653ab4fbd16e50d22fafbb78433e9a36089d032

WORKDIR /usr/src/app

# Bundle App Source
COPY Credfeto.Dispatcher.Server .
COPY appsettings.json .

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT [ "/usr/src/app/Credfeto.Dispatcher.Server" ]

# Perform a healthcheck.  note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/Credfeto.Dispatcher.Server", "--health-check", "http://127.0.0.1:8080/ping?source=docker" ]
