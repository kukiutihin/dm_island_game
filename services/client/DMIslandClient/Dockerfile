FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /build

COPY ["services/client/DMIslandClient/DMIslandClient.fsproj", "src/DMIslandClient/"]
COPY ["services/client/LadaEngine/LadaEngine.fsproj", "src/LadaEngine/"]
RUN dotnet restore "src/DMIslandClient/DMIslandClient.fsproj"

COPY . .
WORKDIR "/build/src/DMIslandClient"
RUN dotnet build "./DMIslandClient.fsproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DMIslandClient.fsproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore --no-build

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DMIslandClient.dll"]
