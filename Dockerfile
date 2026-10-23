FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-azurelinux3.0-distroless-extra@sha256:ec7fcca499ae01c417d515ec29858df225fa76d610c9a9f33ad98f79b72b3938

WORKDIR /usr/src/app

# Bundle App Source
COPY Credfeto.Dispatcher.Server .
COPY appsettings.json .

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT [ "/usr/src/app/Credfeto.Dispatcher.Server" ]

# Perform a healthcheck.  note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/Credfeto.Dispatcher.Server", "--health-check", "http://127.0.0.1:8080/ping?source=docker" ]
