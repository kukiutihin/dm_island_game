FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base
WORKDIR /app

RUN apt-get update && apt-get install -y \
    libglfw3 \
    xvfb \
    libgl1-mesa-dri \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
