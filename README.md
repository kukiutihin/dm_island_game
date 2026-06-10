# DM Island

A turn-based roguelike. The client renders the world with a custom OpenGL engine
and talks to an authoritative game server over HTTP.

## Structure

```
.
├── DMIsland.sln               # solution referencing all projects
├── Directory.Build.props      # shared build settings
├── build.sh                   # build everything
├── run.sh                     # start server + client
├── src/
│   ├── LadaEngine/            # 2D OpenGL engine (OpenTK, F#) — used by the client
│   ├── DMIslandServer/        # authoritative game server (ASP.NET, C#)
│   └── DMIslandClient/        # game client (F#), renders via LadaEngine
└── tests/
    └── DMIslandServer.Tests/  # xUnit tests for the server game logic
```

Project assembly names are unchanged from the original repos
(`LadaEngine`, `RoguelikeServerMVP`, `DMIslandClient`) — only the folder
layout was consolidated, so no namespaces had to change.

## Requirements

- .NET 10 SDK (all projects target `net10.0`).

## Build

```bash
./build.sh
```

## Run

```bash
./run.sh
```

This starts the server on `http://localhost:5229`, waits a moment, then opens
the client window. The client connects to that URL (configured in
`src/DMIslandClient/Scenes/MainMenuScene.fs`). Closing the client stops the
server.

To run them separately, start each from its own folder (the client loads its
textures/fonts using paths relative to the working directory):

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
