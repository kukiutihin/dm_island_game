FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DMIslandClient/DMIslandClient.fsproj", "DMIslandClient/"]
RUN dotnet restore "DMIslandClient/DMIslandClient.fsproj"
COPY . .
WORKDIR "/src/DMIslandClient"
RUN dotnet build "./DMIslandClient.fsproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DMIslandClient.fsproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DMIslandClient.dll"]
