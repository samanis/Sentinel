FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY samples/IncidentLab.TelemetryGenerator/IncidentLab.TelemetryGenerator.csproj samples/IncidentLab.TelemetryGenerator/
RUN dotnet restore samples/IncidentLab.TelemetryGenerator/IncidentLab.TelemetryGenerator.csproj

COPY samples/IncidentLab.TelemetryGenerator/ samples/IncidentLab.TelemetryGenerator/
RUN dotnet publish samples/IncidentLab.TelemetryGenerator/IncidentLab.TelemetryGenerator.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "IncidentLab.TelemetryGenerator.dll"]
