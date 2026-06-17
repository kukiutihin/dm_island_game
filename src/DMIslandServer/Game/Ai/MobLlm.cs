using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RoguelikeServerMVP.Game.Ai;

public static class MobLlm
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly HttpClient InsecureHttp = CreateInsecure();
    private static bool _envLoaded;

    private static HttpClient CreateInsecure()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
    }

    public static string? Decide(string system, string user)
    {
        try
        {
            EnsureEnv();
            var provider = (Env("LLM_PROVIDER") ?? "yandexgpt").Trim().ToLowerInvariant();
            var reply = provider == "gigachat" ? GigaChat(system, user) : Yandex(system, user);
            Console.WriteLine($"[MobLlm/{provider}] reply: {(reply is null ? "<null>" : reply.Trim())}");
            return reply;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[MobLlm] error: {e.Message}");
            return null;
        }
    }

    private static string? Yandex(string system, string user)
    {
        var key = Env("YANDEX_API_KEY");
        var folder = Env("YANDEX_FOLDER_ID");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(folder)) return null;
        var model = Env("YANDEX_MODEL") ?? "yandexgpt/latest";

        var body = new
        {
            modelUri = $"gpt://{folder}/{model}",
            completionOptions = new { stream = false, temperature = 0.4, maxTokens = 30 },
            messages = new object[]
            {
                new { role = "system", text = system },
                new { role = "user", text = user },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post,
            "https://llm.api.cloud.yandex.net/foundationModels/v1/completion");
        req.Headers.TryAddWithoutValidation("Authorization", $"Api-Key {key}");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = Http.Send(req);
        if (!resp.IsSuccessStatusCode) return null;
        using var stream = resp.Content.ReadAsStream();
        using var doc = JsonDocument.Parse(stream);
        var alts = doc.RootElement.GetProperty("result").GetProperty("alternatives");
        if (alts.GetArrayLength() == 0) return null;
        return alts[0].GetProperty("message").GetProperty("text").GetString();
    }

    private static string? GigaChat(string system, string user)
    {
        var token = GigaToken();
        if (token == null) return null;
        var model = Env("GIGACHAT_MODEL") ?? "GigaChat:latest";

        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
            temperature = 0.4,
            max_tokens = 30,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post,
            "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = InsecureHttp.Send(req);
        if (!resp.IsSuccessStatusCode) return null;
        using var stream = resp.Content.ReadAsStream();
        using var doc = JsonDocument.Parse(stream);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0) return null;
        return choices[0].GetProperty("message").GetProperty("content").GetString();
    }

    private static string? GigaToken()
    {
        var id = Env("GIGACHAT_CLIENT_ID");
        var secret = Env("GIGACHAT_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret)) return null;
        var scope = Env("GIGACHAT_SCOPE") ?? "GIGACHAT_API_PERS";
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));

        using var req = new HttpRequestMessage(HttpMethod.Post,
            "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
        req.Headers.TryAddWithoutValidation("Authorization", $"Basic {basic}");
        req.Headers.TryAddWithoutValidation("RqUID", Guid.NewGuid().ToString());
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        req.Content = new StringContent($"scope={scope.Trim()}", Encoding.UTF8, "application/x-www-form-urlencoded");

        using var resp = InsecureHttp.Send(req);
        if (!resp.IsSuccessStatusCode) return null;
        using var stream = resp.Content.ReadAsStream();
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static void EnsureEnv()
    {
        if (_envLoaded) return;
        _envLoaded = true;
        if (!string.IsNullOrWhiteSpace(Env("LLM_PROVIDER"))) return;

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir != null; i++)
        {
            var candidate = Path.Combine(dir, "services", "agent-runner", ".env");
            if (File.Exists(candidate)) { LoadEnvFile(candidate); return; }
            dir = Directory.GetParent(dir)?.FullName;
        }
    }

    private static void LoadEnvFile(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (Environment.GetEnvironmentVariable(key) == null)
                Environment.SetEnvironmentVariable(key, val);
        }
    }
}
