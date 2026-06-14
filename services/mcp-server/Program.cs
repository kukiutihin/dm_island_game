using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var gameUrl = Environment.GetEnvironmentVariable("GAME_SERVICE_URL") ?? "http://localhost:5555";
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
        await writer.WriteLineAsync(response);
        await writer.FlushAsync();
    }
}

static async Task<string> ProcessMessage(string raw, GameClient game)
{
    try
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
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
        if (method == "notifications/initialized")
        {
            return "";
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
        new {
            name = "restart",
            description = "Restart the game from floor 1",
            inputSchema = new { type = "object", properties = new { }, required = new string[0] }
        },
        new {
            name = "get_state",
            description = "Get the current game state (player, enemies, items, map)",
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
    var root = doc.RootElement;
    var filtered = new Dictionary<string, object?>
    {
        ["turn"] = root.TryGetProperty("turn", out var t) ? t.GetInt32() : 0,
        ["floor"] = root.TryGetProperty("floor", out var f) ? f.GetInt32() : 0,
        ["completed"] = root.TryGetProperty("completed", out var c) ? c.GetBoolean() : false,
        ["player"] = root.TryGetProperty("player", out var p) ? ParsePlayer(p) : null,
        ["entities"] = root.TryGetProperty("entities", out var e) ? ParseEntities(e) : null,
        ["items"] = root.TryGetProperty("items", out var i) ? i.EnumerateArray().Select(x => x.GetString()).ToList() : null
    };
    return JsonSerializer.Serialize(filtered);
}

static object? ParsePlayer(JsonElement p)
{
    return new
    {
        hp = p.GetProperty("hp").GetInt32(),
        maxHp = p.GetProperty("maxHp").GetInt32(),
        position = new
        {
            x = p.GetProperty("position").GetProperty("x").GetInt32(),
            y = p.GetProperty("position").GetProperty("y").GetInt32()
        }
    };
}

static object? ParseEntities(JsonElement arr)
{
    return arr.EnumerateArray().Select(e => new
    {
        type = e.GetProperty("type").GetString(),
        hp = e.GetProperty("hp").GetInt32(),
        position = new
        {
            x = e.GetProperty("position").GetProperty("x").GetInt32(),
            y = e.GetProperty("position").GetProperty("y").GetInt32()
        }
    }).ToList();
}

static string Error(string msg)
{
    return JsonSerializer.Serialize(new { error = msg });
}
