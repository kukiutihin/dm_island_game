using System.Net.Http.Json;
using System.Text.Json;

class GameClient
{

    HttpClient GameClient_;
    public GameClient(string baseUrl) => GameClient_ =  new HttpClient { BaseAddress = new Uri(baseUrl) };
    public async Task<JsonDocument> PostAction(string act, string? dir)
    {
        var body = new {action = act , direction = dir};
        var json = await GameClient_.PostAsJsonAsync("/action",body);
        return JsonDocument.Parse(await json.Content.ReadAsStringAsync());
    }
    public async Task<JsonDocument> GetState()
    {
        var json = await GameClient_.GetStringAsync("/state");
        return JsonDocument.Parse(json);
    }
}


