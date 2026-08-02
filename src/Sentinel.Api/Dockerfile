FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["global.json", "."]
COPY ["src/Sentinel.Api/Sentinel.Api.csproj", "src/Sentinel.Api/"]
COPY ["src/Sentinel.Application/Sentinel.Application.csproj", "src/Sentinel.Application/"]
COPY ["src/Sentinel.Domain/Sentinel.Domain.csproj", "src/Sentinel.Domain/"]
COPY ["src/Sentinel.Infrastructure/Sentinel.Infrastructure.csproj", "src/Sentinel.Infrastructure/"]
RUN dotnet restore "src/Sentinel.Api/Sentinel.Api.csproj"

COPY ["src/", "src/"]
RUN dotnet publish "src/Sentinel.Api/Sentinel.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Sentinel.Api.dll"]
