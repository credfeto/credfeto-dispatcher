FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra@sha256:661d3e8cfdab9503453095a5b53b17f63402400266c0e0f6f2d1b1192e6da5c9

WORKDIR /usr/src/app

# Bundle App Source
COPY Credfeto.Dispatcher.Server .
COPY appsettings.json .

# Fail the image build, not production, if the binary cannot start in this runtime base
# (glibc, ICU, OpenSSL or DI problems). Exec form: the chiseled image has no shell.
RUN [ "/usr/src/app/Credfeto.Dispatcher.Server", "--self-test" ]

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT [ "/usr/src/app/Credfeto.Dispatcher.Server" ]

# Perform a healthcheck.  note that ECS ignores this, so this is for local development
HEALTHCHECK --interval=5s --timeout=2s --retries=3 --start-period=5s CMD [ "/usr/src/app/Credfeto.Dispatcher.Server", "--health-check", "http://127.0.0.1:8080/ping?source=docker" ]
