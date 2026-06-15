using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// Defaults to the local game server port; compose overrides this with the service URL.
var gameUrl = Environment.GetEnvironmentVariable("GAME_SERVICE_URL") ?? "http://localhost:5229";
var game = new GameClient(gameUrl);
var useStdio = args.Length > 0 && args[0] == "--stdio";

if (useStdio)
{
    Console.Error.WriteLine("MCP server starting in stdio mode");
    await HandleStream(Console.In, Console.Out, game);
}
else
{
    var port = int.Parse(Environment.GetEnvironmentVariable("MCP_PORT") ?? "5000");
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();
    Console.Error.WriteLine($"MCP server listening on TCP port {port}");
    while (true)
    {
        var tcp = await listener.AcceptTcpClientAsync();
        _ = HandleTcpConnection(tcp, game);
    }
}

static async Task HandleTcpConnection(TcpClient tcp, GameClient game)
{
    using var stream = tcp.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    using var writer = new StreamWriter(stream, Encoding.UTF8) { NewLine = "\n" };
    await HandleStream(reader, writer, game);
}

static async Task HandleStream(TextReader reader, TextWriter writer, GameClient game)
{
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        var response = await ProcessMessage(line, game);
        // Notifications (no id) return null and must not produce a response line.
        if (response is null) continue;
        await writer.WriteLineAsync(response);
        await writer.FlushAsync();
    }
}

static async Task<string?> ProcessMessage(string raw, GameClient game)
{
    try
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        // Echo the id back verbatim: JSON-RPC ids may be a string OR a number, so we
        // must not coerce with GetString() (which throws on numeric ids).
        object? id = root.TryGetProperty("id", out var idProp) ? (object)idProp : null;
        var method = root.GetProperty("method").GetString();
        if (method == "tools/list")
        {
            var tools = GetToolList();
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                result = new { tools }
            });
        }
        if (method == "tools/call")
        {
            var args = root.GetProperty("params");
            var name = args.GetProperty("name").GetString();
            var arguments = args.TryGetProperty("arguments", out var a) ? a : default;
            var result = await ExecuteTool(name, arguments, game);
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                result = new { content = new[] { new { type = "text", text = result } } }
            });
        }
        if (method == "initialize")
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "dm-island-mcp", version = "1.0.0" }
                }
            });
        }
        if (method is not null && method.StartsWith("notifications/"))
        {
            return null;
        }
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new { code = -32601, message = $"Method not found: {method}" }
        });
    }
    catch (Exception ex)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = (string?)null,
            error = new { code = -32700, message = ex.Message }
        });
    }
}

static object[] GetToolList()
{
    return
    [
        new {
            name = "move",
            description = "Move player in a direction",
            inputSchema = new {
                type = "object",
                properties = new {
                    direction = new {
                        type = "string",
                        @enum = new[] { "up", "down", "left", "right" }
                    }
                },
                required = new[] { "direction" }
            }
        },
        new {
            name = "attack",
            description = "Attack in a direction",
            inputSchema = new {
                type = "object",
                properties = new {
                    direction = new {
                        type = "string",
                        @enum = new[] { "up", "down", "left", "right" }
                    }
                },
                required = new[] { "direction" }
            }
        },
        new {
            name = "skip_turn",
            description = "Skip the current turn (do nothing)",
            inputSchema = new { type = "object", properties = new { }, required = new string[0] }
        },
        // Note: "restart" is intentionally NOT advertised to the agent (so it can't reset
        // its own game), but ExecuteTool still handles it for the harness between games.
        new {
            name = "get_state",
            description = "Get the current game state: player (hp, position), visible entities (mobs/projectiles), static objects (walls, exit portal), and collected items",
            inputSchema = new { type = "object", properties = new { }, required = new string[0] }
        },
        new {
            name = "get_inventory",
            description = "Get the player's inventory",
            inputSchema = new { type = "object", properties = new { }, required = new string[0] }
        }
    ];
}

static async Task<string> ExecuteTool(string? name, JsonElement? args, GameClient game)
{
    switch (name)
    {
        case "move":
        {
            var dir = args?.GetProperty("direction").GetString();
            if (dir is null || !new[] { "up", "down", "left", "right" }.Contains(dir))
                return Error("Invalid direction. Must be: up, down, left, right");
            var state = await game.PostAction("move", dir);
            return FilterState(state);
        }
        case "attack":
        {
            var dir = args?.GetProperty("direction").GetString();
            if (dir is null || !new[] { "up", "down", "left", "right" }.Contains(dir))
                return Error("Invalid direction. Must be: up, down, left, right");
            var state = await game.PostAction("attack", dir);
            return FilterState(state);
        }
        case "skip_turn":
        {
            var state = await game.PostAction("skip", null);
            return FilterState(state);
        }
        case "restart":
        {
            var state = await game.PostAction("restart", null);
            return FilterState(state);
        }
        case "get_state":
        {
            var state = await game.GetState();
            return FilterState(state);
        }
        case "get_inventory":
        {
            var state = await game.GetState();
            var items = state.RootElement.TryGetProperty("items", out var itemsProp)
                ? itemsProp.ToString()
                : "[]";
            return items;
        }
        default:
            return Error($"Unknown tool: {name}");
    }
}

static string FilterState(JsonDocument doc)
{
    var dirs = new (string Dir, int Dx, int Dy)[]
        { ("up", 0, -1), ("down", 0, 1), ("left", -1, 0), ("right", 1, 0) };
    var enemyTypes = new HashSet<string>
        { "ModusPonens", "Lambda", "Monad", "Nerd", "NuclearNerd", "Skolem", "Mole" };

    var root = doc.RootElement;

    var (px, py) = root.TryGetProperty("player", out var pl) ? ReadXY(pl) : (0, 0);

    // Collect this room's wall tiles, and the tiles occupied by living enemies (they block moves).
    var walls = new HashSet<(int X, int Y)>();
    if (root.TryGetProperty("objects", out var objs) && objs.ValueKind == JsonValueKind.Array)
        foreach (var ob in objs.EnumerateArray())
            if (ReadString(ob, "type") == "Wall" && TryXY(ob, out var w))
                walls.Add(w);

    var enemyTiles = new HashSet<(int X, int Y)>();
    if (root.TryGetProperty("entities", out var ents) && ents.ValueKind == JsonValueKind.Array)
        foreach (var en in ents.EnumerateArray())
            if (enemyTypes.Contains(ReadString(en, "type") ?? "") && ReadInt(en, "hp") > 0 && TryXY(en, out var et))
                enemyTiles.Add(et);

    // Valid moves: directions where the next tile isn't a wall or a living enemy.
    var validMoves = dirs
        .Where(d => !walls.Contains((px + d.Dx, py + d.Dy)) && !enemyTiles.Contains((px + d.Dx, py + d.Dy)))
        .Select(d => d.Dir)
        .ToList();

    // Doors = gaps in the wall border (for the navigation hints).
    var (doors, _interior) = DoorsAndInteriorWalls(walls);

    // Send the FULL wall map (border + interior) so the agent can pathfind AROUND interior
    // obstacles to reach a door/exit. The agent uses these only for routing — the LLM prompt
    // is still summarised via validMoves/doors, so this doesn't bloat the model's context.
    var slimObjects = new List<object>();
    foreach (var w in walls)
        slimObjects.Add(new { type = "Wall", position = new { x = w.X, y = w.Y } });
    if (root.TryGetProperty("objects", out var objs2) && objs2.ValueKind == JsonValueKind.Array)
        foreach (var ob in objs2.EnumerateArray())
            if (ReadString(ob, "type") != "Wall")
                slimObjects.Add(new { type = ReadString(ob, "type"), position = ReadPos(ob) });

    var filtered = new Dictionary<string, object?>
    {
        ["turn"] = ReadInt(root, "turn"),
        ["floor"] = ReadInt(root, "floor"),
        ["completed"] = root.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.True,
        ["player"] = root.TryGetProperty("player", out var p) ? ParsePlayer(p) : null,
        // Visible mobs/projectiles (and floor pickups) the server reports near the player.
        ["entities"] = root.TryGetProperty("entities", out var e) ? ParseEntities(e) : new List<object>(),
        // Interior walls + the exit portal only (the room border is summarised by doors/validMoves).
        ["objects"] = slimObjects,
        // Directions the player can actually step right now.
        ["validMoves"] = validMoves,
        // Gaps in the wall border — the room's doorways (x, y, side).
        ["doors"] = doors,
        // Floor minimap: which rooms exist, are cleared/current, and which is the exit.
        ["rooms"] = root.TryGetProperty("rooms", out var rm) ? ParseRooms(rm) : new List<object>(),
        // Inventory: items the player has collected.
        ["items"] = root.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array
            ? i.EnumerateArray().Select(x => x.GetString()).ToList()
            : new List<string?>()
    };
    return JsonSerializer.Serialize(filtered);
}

/// <summary>Splits the wall set into doorway gaps (on the border ring) and interior walls.</summary>
static (List<object> doors, List<(int X, int Y)> interior) DoorsAndInteriorWalls(HashSet<(int X, int Y)> walls)
{
    var doors = new List<object>();
    var interior = new List<(int X, int Y)>();
    if (walls.Count == 0) return (doors, interior);

    int minX = walls.Min(w => w.X), maxX = walls.Max(w => w.X);
    int minY = walls.Min(w => w.Y), maxY = walls.Max(w => w.Y);

    foreach (var w in walls)
        if (w.X != minX && w.X != maxX && w.Y != minY && w.Y != maxY)
            interior.Add(w);

    // A non-wall tile on an edge (between the corners) is a doorway.
    for (var x = minX + 1; x < maxX; x++)
    {
        if (!walls.Contains((x, minY))) doors.Add(new { x, y = minY, side = "up" });
        if (!walls.Contains((x, maxY))) doors.Add(new { x, y = maxY, side = "down" });
    }
    for (var y = minY + 1; y < maxY; y++)
    {
        if (!walls.Contains((minX, y))) doors.Add(new { x = minX, y, side = "left" });
        if (!walls.Contains((maxX, y))) doors.Add(new { x = maxX, y, side = "right" });
    }
    return (doors, interior);
}

static object? ParsePlayer(JsonElement p) =>
    new { hp = ReadInt(p, "hp"), maxHp = ReadInt(p, "maxHp"), position = ReadPos(p) };

static object ParseEntities(JsonElement arr)
{
    if (arr.ValueKind != JsonValueKind.Array) return new List<object>();
    return arr.EnumerateArray()
        .Select(e => (object)new { id = ReadString(e, "id"), type = ReadString(e, "type"), hp = ReadInt(e, "hp"), position = ReadPos(e) })
        .ToList();
}

static object ParseRooms(JsonElement arr)
{
    if (arr.ValueKind != JsonValueKind.Array) return new List<object>();
    return arr.EnumerateArray()
        .Select(r => (object)new
        {
            x = ReadInt(r, "x"),
            y = ReadInt(r, "y"),
            visited = ReadBool(r, "visited"),
            cleared = ReadBool(r, "cleared"),
            current = ReadBool(r, "current"),
            isExit = ReadBool(r, "isExit"),
        })
        .ToList();
}

// --- Defensive readers: never throw on a missing/oddly-typed field. ---

static int ReadInt(JsonElement el, string name) =>
    el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

static bool ReadBool(JsonElement el, string name) =>
    el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

static string? ReadString(JsonElement el, string name) =>
    el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

static object? ReadPos(JsonElement el) =>
    el.TryGetProperty("position", out var pos)
        ? new { x = ReadInt(pos, "x"), y = ReadInt(pos, "y") }
        : null;

static (int X, int Y) ReadXY(JsonElement el) =>
    el.TryGetProperty("position", out var pos) ? (ReadInt(pos, "x"), ReadInt(pos, "y")) : (0, 0);

static bool TryXY(JsonElement el, out (int X, int Y) xy)
{
    if (el.TryGetProperty("position", out var pos))
    {
        xy = (ReadInt(pos, "x"), ReadInt(pos, "y"));
        return true;
    }
    xy = (0, 0);
    return false;
}

static string Error(string msg)
{
    return JsonSerializer.Serialize(new { error = msg });
}
