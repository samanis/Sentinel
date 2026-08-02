FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["global.json", "."]
COPY ["samples/IncidentLab.OrderApi/IncidentLab.OrderApi.csproj", "samples/IncidentLab.OrderApi/"]
RUN dotnet restore "samples/IncidentLab.OrderApi/IncidentLab.OrderApi.csproj"

COPY ["samples/IncidentLab.OrderApi/", "samples/IncidentLab.OrderApi/"]
RUN dotnet publish "samples/IncidentLab.OrderApi/IncidentLab.OrderApi.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IncidentLab.OrderApi.dll"]
