FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY IsoDocument.Api.csproj ./
RUN dotnet restore IsoDocument.Api.csproj

COPY . .
RUN dotnet publish IsoDocument.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -g 1654 isoapp \
    && useradd -u 1654 -g isoapp -M -s /usr/sbin/nologin isoapp

ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Taipei

COPY --from=build /app/publish .

RUN mkdir -p /app/storage-data \
    && chown -R isoapp:isoapp /app

USER isoapp
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "IsoDocument.Api.dll"]
