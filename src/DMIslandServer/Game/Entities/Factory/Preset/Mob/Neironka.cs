using System.Threading.Tasks;
using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Ai;
using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

public class Neironka(Position position) : Entities.Mob(EntityType.Neironka, position, BaseHp)
{
    private const int BaseHp = 36;

    private const int ShieldDuration = 4;
    private const int ShieldHealPerTurn = 2;
    private const int ShieldBlastRadius = 2;
    private const int ShieldBlastDamage = 2;
    private const int ShieldCooldown = 14;
    private const int BombCooldown = 8;
    private const int MinFuse = 2;
    private const int MaxFuse = 5;

    private const string SystemPrompt =
        "You are Neironka, an elite boss monster in a roguelike dungeon. A hero is trying to " +
        "kill you. Your goal: defeat the hero while staying alive.\n" +
        "Every turn you choose ONE skill, and you MAY also add a free teleport.\n" +
        "Skills:\n" +
        "- attack: scatter mines across the room; they explode after a few turns and hurt the hero " +
        "if he stands on them. Good to pressure the hero, especially from a distance.\n" +
        "- shield: raise a shield for a few turns — you take NO damage and heal a little, AND release a " +
        "shockwave that DAMAGES the hero if he is within 2 tiles of you. So when the hero is adjacent, " +
        "shield both blocks his hits and hurts him; also great when your HP is low.\n" +
        "- teleport: right AFTER using a skill you may blink the SAME turn for FREE (no cooldown). " +
        "You choose where: 'teleport away' = flee far from the hero, 'teleport near' = jump right next " +
        "to the hero (to threaten him and line up your next shield shockwave), plain 'teleport' = a " +
        "random spot. Use it often to dodge and reposition.\n" +
        "Each skill has a cooldown, and you cannot attack while your previous mines are still ticking; " +
        "if your skill is unavailable you simply wait.\n" +
        "Reply with ONE skill word (attack or shield), and optionally add a teleport. " +
        "Examples: 'attack', 'shield', 'attack teleport away', 'shield teleport near', 'attack teleport'.";

    private Task<string?>? _inFlight;
    private int _cdBomb;
    private int _cdShield;
    private int _hideTurns;
    private string _lastVisual = "idle";

    private bool Shielded => _hideTurns > 0;

    public override void TakeDamage(int amount, GameState state)
    {
        if (Shielded) return;
        base.TakeDamage(amount, state);
    }

    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);

        if (_cdBomb > 0) _cdBomb--;
        if (_cdShield > 0) _cdShield--;

        if (_hideTurns > 0)
        {
            if (Hp < MaxHp) Heal(System.Math.Min(ShieldHealPerTurn, MaxHp - Hp));
            if (--_hideTurns == 0)
            {
                _cdShield = ShieldCooldown;
                EmitVisual(state, "idle");
            }
            return;
        }

        if (_inFlight == null)
        {
            var prompt = BuildPrompt(state);
            _inFlight = Task.Run(() => MobLlm.Decide(SystemPrompt, prompt));
            return;
        }

        if (!_inFlight.IsCompleted) return;

        var reply = _inFlight.IsCompletedSuccessfully ? _inFlight.Result : null;
        _inFlight = null;
        Execute(state, reply);
    }

    private void Execute(GameState state, string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            System.Console.WriteLine("[Neironka] no LLM reply -> wait");
            EmitVisual(state, "idle");
            return;
        }

        var t = reply.ToLowerInvariant();
        var wantsTeleport = t.Contains("teleport") || t.Contains("blink");
        var mode = TeleportMode(t);
        var skill = Classify(reply);
        var used = skill == "shield" ? TryShield(state) : TryBomb(state);

        System.Console.WriteLine(
            $"[Neironka] LLM: \"{reply.Trim()}\" -> {skill}{(wantsTeleport ? $"+teleport({mode})" : "")} used={used}");

        if (used && wantsTeleport) Teleport(state, mode);
    }

    private static string TeleportMode(string t)
    {
        if (t.Contains("away") || t.Contains("far") || t.Contains("flee") || t.Contains("escape")) return "away";
        if (t.Contains("near") || t.Contains("close")) return "near";
        return "random";
    }

    private bool TryShield(GameState state)
    {
        if (_cdShield > 0) return false;
        if (Position.SquaredDistanceTo(state.Player.Position) <= ShieldBlastRadius * ShieldBlastRadius)
            state.Player.TakeDamage(ShieldBlastDamage, state);
        _hideTurns = ShieldDuration;
        EmitVisual(state, "hide");
        return true;
    }

    private bool TryBomb(GameState state)
    {
        if (_cdBomb > 0 || BombsLive(state)) return false;
        CarpetBomb(state);
        _cdBomb = BombCooldown;
        EmitVisual(state, "attack");
        return true;
    }

    private static string Classify(string? reply)
    {
        var t = (reply ?? "").ToLowerInvariant();
        if (t.Contains("shield") || t.Contains("defend")) return "shield";
        return "attack";
    }

    private static bool BombsLive(GameState state) =>
        state.Projectiles.Any(p => p.IsAlive && p.Type == EntityType.NeironkaBomb)
        || state.DelayedProjectiles.Any(p => p.IsAlive && p.Type == EntityType.NeironkaBomb);

    private string BuildPrompt(GameState state)
    {
        var p = state.Player;
        var (dir, dist) = RelativeTo(p.Position);
        return $"Your HP: {Hp}/{MaxHp}. Hero HP: {p.Hp}/{p.MaxHp}. Hero is {dir}, {dist} tiles away. " +
               $"attack ready: {(_cdBomb == 0 && !BombsLive(state) ? "yes" : "no")}. " +
               $"shield ready: {(_cdShield == 0 ? "yes" : "no")}. " +
               $"Pick a skill (attack or shield); add 'teleport' to also blink for free.";
    }

    private (string dir, int dist) RelativeTo(Position target)
    {
        var dx = target.X - Position.X;
        var dy = target.Y - Position.Y;
        var dist = System.Math.Abs(dx) + System.Math.Abs(dy);
        if (dx == 0 && dy == 0) return ("right on top of you", 0);
        string dir = System.Math.Abs(dx) >= System.Math.Abs(dy)
            ? (dx > 0 ? "to your right" : "to your left")
            : (dy > 0 ? "below you" : "above you");
        return (dir, dist);
    }

    private void Teleport(GameState state, string mode)
    {
        var room = state.GetCurrentRoom();
        var rand = state.GetRandom().Random;

        if (mode == "random")
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var p = new Position(rand.Next(room.Width), rand.Next(room.Height));
                if (state.CanMoveTo(p)) { BlinkTo(state, p); return; }
            }
            return;
        }

        var hero = state.Player.Position;
        Position? best = null;
        var bestScore = mode == "away" ? int.MinValue : int.MaxValue;
        for (var x = 0; x < room.Width; x++)
        for (var y = 0; y < room.Height; y++)
        {
            var p = new Position(x, y);
            if (!state.CanMoveTo(p)) continue;
            var d = p.SquaredDistanceTo(hero);
            if (mode == "near" && d == 0) continue;
            if (mode == "away" ? d > bestScore : d < bestScore)
            {
                bestScore = d;
                best = p;
            }
        }
        if (best.HasValue) BlinkTo(state, best.Value);
    }

    private void BlinkTo(GameState state, Position p)
    {
        Position = p;
        PreviousPosition = p;
        state.AddEvent(new Event(EventType.NeironkaBoom, p, Id.ToString()));
    }

    private void CarpetBomb(GameState state)
    {
        var room = state.GetCurrentRoom();
        var rand = state.GetRandom().Random;
        foreach (var p in NeironkaPatterns.Choose(rand, room.Width, room.Height))
            if (state.CanMoveTo(p) || p == state.Player.Position)
                state.AddProjectile(new NeironkaBomb(p, rand.Next(MinFuse, MaxFuse + 1)));
    }

    private void EmitVisual(GameState state, string kind)
    {
        if (kind == _lastVisual) return;
        _lastVisual = kind;
        state.AddEvent(new Event(EventType.NeironkaVisual, Position, $"{Id}|{kind}"));
    }
}
