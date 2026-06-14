# DM Island

A turn-based roguelike. The client renders the world with a custom OpenGL engine
and talks to an authoritative game server over HTTP.

## Structure

```
.
├── DMIsland.sln               # solution referencing all projects
├── Directory.Build.props      # shared build settings
├── docker-compose.yml         # build + run the server in Docker
├── src/
│   ├── LadaEngine/            # 2D OpenGL engine (OpenTK, F#) — used by the client
│   ├── DMIslandServer/        # authoritative game server (ASP.NET, C#) + Dockerfile
│   └── DMIslandClient/        # game client (F#), renders via LadaEngine
└── tests/
    └── DMIslandServer.Tests/  # xUnit tests for the server game logic
```

Project assembly names are unchanged from the original repos
(`LadaEngine`, `RoguelikeServerMVP`, `DMIslandClient`) — only the folder
layout was consolidated, so no namespaces had to change.

## Requirements

- Docker (with Compose v2) — to run the server.
- .NET 10 SDK — to run the client and the tests (all projects target `net10.0`).

## Run

The server runs in Docker; the client is a desktop OpenGL window, so it runs on
your host machine.

```bash
# 1) build + start the server (http://localhost:5229)
docker compose up --build        # add -d to run in the background

# 2) in another terminal, start the client (connects to localhost:5229)
dotnet run --project src/DMIslandClient
```

Stop the server with `docker compose down` (or Ctrl-C if running in the
foreground). The client connects to `http://localhost:5229`, configured in
`src/DMIslandClient/Scenes/MainMenuScene.fs`.

> The client can't run in a container on macOS/Windows because it needs a
> display and the GPU. `docker-compose.yml` includes a commented-out, Linux-only
> X11 client service if you want to containerize it on a Linux host.

### Running without Docker

The server is a plain ASP.NET app, so you can also run both with the SDK:

```bash
# terminal 1 — server
cd src/DMIslandServer && dotnet run

# terminal 2 — client
cd src/DMIslandClient && dotnet run
```

## Tests

```bash
dotnet test
```

## Level generation

The server now generates each room procedurally
(`src/DMIslandServer/Game/Generation/RoomGenerators.cs`). A seeded
cellular-automata cave generator produces the walkable layout; the player and
mobs are snapped to valid floor tiles, and walls are emitted as entities so the
client renders them.

Configure it in `src/DMIslandServer/appsettings.json`:

```jsonc
"UseProceduralGeneration": true,  // false => empty room with a border wall
"Seed": 12345                     // change for a different layout
```

The multi-room `Level3x3` floor generator from the `feature/room-generation`
branch is **not** wired in yet — it needs the server's `GameState` to support
room-to-room transitions. See the integration notes if you want that next.

## Architecture

```
client (F#)  ──HTTP /action──▶  server (C#, authoritative turn logic)
        ▲                              │
        └──────── game state ──────────┘
both rendered/powered by LadaEngine (client side)
```

Each `POST /action` (`move` / `attack` / `skip`, with a direction) advances one
turn: the player acts, mobs take their turn (chase within aggro range, attack
when adjacent, otherwise wander), then the server returns the slice of the world
visible around the player.
