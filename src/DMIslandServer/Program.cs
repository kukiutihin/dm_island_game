using System.Text.Json;
using RoguelikeServerMVP;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game;
using RoguelikeServerMVP.Game.Generation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Конфиг игры (из appsettings.json)
var gameConfig = new GameConfig();
builder.Configuration.GetSection("GameConfig").Bind(gameConfig);
builder.Services.AddSingleton(gameConfig);

// Регистрация игрового движка (одна сессия)
builder.Services.AddSingleton<GameEngine>(sp =>
{
    var config = sp.GetRequiredService<GameConfig>();
    return new GameEngine(config);
});

var app = builder.Build();

// Endpoint /action
app.MapPost("/action", (PlayerActionRequest request, GameEngine engine) =>
{
    var action = request.Action?.ToLowerInvariant();

    Direction? dir = null;
    if (!string.IsNullOrWhiteSpace(request.Direction))
    {
        dir = request.Direction.ToLowerInvariant() switch
        {
            "up"    => Direction.Up,
            "down"  => Direction.Down,
            "left"  => Direction.Left,
            "right" => Direction.Right,
            _       => null
        };
    }

    switch (action)
    {
        case "move":
            if (dir.HasValue)
                engine.PlayerMove(dir.Value);
            break;
        case "attack":
            if (dir.HasValue)
                engine.PlayerAttack(dir.Value);
            break;
        case "skip":
            engine.PlayerSkipTurn();
            break;
        case "restart":
            // Start a fresh run from floor 1 with a healed player (respawn after death).
            engine.Restart();
            break;
        default:
            // неизвестное действие — ничего не делаем
            Console.WriteLine($"Unknown action: {action}");
            break;
    }

    var response = BuildGameStateResponse(engine.State, engine.Config, engine.Floor);
    return Results.Json(response);
});

// Endpoint /state — read-only snapshot of the current game (no turn advance).
// Used by the MCP server's get_state / get_inventory tools.
app.MapGet("/state", (GameEngine engine) =>
{
    var response = BuildGameStateResponse(engine.State, engine.Config, engine.Floor);
    return Results.Json(response);
});

app.Run();

// --------- вспомогательная функция ---------

static GameStateResponse BuildGameStateResponse(GameState state, GameConfig config, RoguelikeServerMVP.Game.Dungeon.Floor floor)
{
    var viewWidth = config.ViewWidth;
    var viewHeight = config.ViewHeight;
    // Large enough to cover the enlarged exit room corner-to-corner so its walls/objects don't pop in.
    var viewRadius = 26;

    var player = state.Player;

    var resp = new GameStateResponse
    {
        Turn = state.TurnNumber,
        ViewWidth = viewWidth,
        ViewHeight = viewHeight,
        Floor = floor.Number,
        Completed = floor.Number >= config.MaxFloors && floor.AllCleared,
        Room = new RoomDto
        {
            Id = state.GetCurrentRoom().Id,
            Biome = floor.Current.Biome,
            Width = floor.Current.Width,
            Height = floor.Current.Height
        },
        Rooms = floor.AllRooms.Select(r => new RoomCellDto
        {
            X = r.GridX,
            Y = r.GridY,
            Visited = r.Visited,
            Cleared = r.Cleared,
            Current = r.GridX == floor.CurrentX && r.GridY == floor.CurrentY,
            // Only mark the exit on the minimap once it's open (whole floor cleared).
            IsExit = r.IsExit && floor.AllCleared
        }).ToList(),
        Player = new ObjectViewDto
        {
            Type = EntityType.Player,
            Id = player.Id,
            Hp = player.Hp,
            MaxHp = player.MaxHp,
            Position = new PositionDto(player.Position),
            PreviousPosition = new PositionDto(player.PreviousPosition),
        },
        Items = player.GetItems(),
        Objects = new List<ObjectViewDto>()
    };

    // 1) Мобы
    var allEntities = state.Mobs.Concat<Entity>(state.Projectiles).Concat(state.Items);
    foreach (var mob in allEntities)
    {
        if (!mob.IsAlive) continue;
        
        if (mob.Position.SquaredDistanceTo(player.Position) < viewRadius * viewRadius)
        {
            resp.Entities.Add(new ObjectViewDto
            {
                Id = mob.Id,
                Type = mob.Type,
                Hp = mob.Hp,
                MaxHp = mob.MaxHp,
                Position = new PositionDto(mob.Position),
                PreviousPosition = new PositionDto(mob.PreviousPosition)
            });
        }
    }

    // 2) Стены и другие статические объекты
    foreach (var obj in state.StaticObjects)
    {
        if (!obj.IsAlive) continue;
        
        if (obj.Position.SquaredDistanceTo(player.Position) < viewRadius * viewRadius)
        {
            resp.Objects.Add(new ObjectViewDto
            {
                Id = obj.Id,
                Type = obj.Type,
                Hp = obj.Hp,
                MaxHp = obj.MaxHp,
                Position = new PositionDto(obj.Position),
                PreviousPosition = new PositionDto(obj.Position)
            });
        }
    }
    
    // 3) События
    while (state.EventQueue.Count > 0)
    {
        var e = state.EventQueue.Dequeue();

        resp.Events.Add(new EventDto
        {
            Position = e.Position,
            Type = e.Type,
            Payload = e.Payload,
        });
    }

    return resp;
}