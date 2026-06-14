# Build the authoritative game server.
# Build context is the repository root (see docker-compose.yml) so that the
# shared Directory.Build.props is available to MSBuild.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore first (cached unless the project file changes).
COPY Directory.Build.props ./
COPY src/DMIslandServer/RoguelikeServerMVP.csproj src/DMIslandServer/
RUN dotnet restore src/DMIslandServer/RoguelikeServerMVP.csproj

# Then build + publish.
COPY src/DMIslandServer/ src/DMIslandServer/
RUN dotnet publish src/DMIslandServer/RoguelikeServerMVP.csproj \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime image: just the ASP.NET runtime + published output.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Listen on all interfaces so the port can be published to the host.
ENV ASPNETCORE_URLS=http://0.0.0.0:5229
EXPOSE 5229

ENTRYPOINT ["dotnet", "RoguelikeServerMVP.dll"]
