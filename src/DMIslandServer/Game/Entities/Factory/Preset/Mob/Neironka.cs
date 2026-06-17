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
    private const int TeleportRadius = 9;
    private const int RingCooldown = 35;
    private const int RingStartRadius = 2;
    private const int RingHold = 3;
    private const int RingDamage = 2;
    private const int RingSpacing = 3;
    private const int RingGaps = 4;
    private const double RingHoleTilesPerSide = 1.0;
    private const double RingReturnChance = 0.4;

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
        "- ring: summon a ring of energy around yourself; it holds for a moment then expands outward one " +
        "tile per turn (up to 10 tiles), damaging the hero wherever it passes through him. Long cooldown — " +
        "great to zone the hero and fill the arena.\n" +
        "- teleport: right AFTER using a skill you may blink the SAME turn for FREE (no cooldown). " +
        "You choose where: 'teleport away' = flee far from the hero, 'teleport near' = jump right next " +
        "to the hero (to threaten him and line up your next shield shockwave), plain 'teleport' = a " +
        "random spot. Use it often to dodge and reposition.\n" +
        "Each skill has a cooldown, and you cannot attack while your previous mines are still ticking; " +
        "if your skill is unavailable you simply wait.\n" +
        "Reply with ONE skill word (attack, shield or ring), and optionally add a teleport. " +
        "Examples: 'attack', 'shield', 'ring', 'attack teleport away', 'shield teleport near'.";

    private Task<string?>? _inFlight;
    private int _cdBomb;
    private int _cdShield;
    private int _cdRing;
    private int _hideTurns;
    private bool _ringActive;
    private bool _ringExpanding;
    private bool _ringReturn;
    private int _ringRadius;
    private int _ringHold;
    private int _ringCount;
    private int _ringMax;
    private double _ringGapOffset;
    private Position _ringCenter;
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
        if (_cdRing > 0) _cdRing--;

        if (_ringActive)
        {
            SpawnRing(state, _ringCenter, _ringRadius);
            if (_ringHold > 0)
                _ringHold--;
            else if (_ringExpanding)
            {
                if (_ringRadius - (_ringCount - 1) * RingSpacing >= _ringMax)
                {
                    if (_ringReturn) _ringExpanding = false;
                    else _ringActive = false;
                }
                else _ringRadius++;
            }
            else
            {
                if (_ringRadius <= 0) _ringActive = false;
                else _ringRadius--;
            }
            return;
        }

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
        var used = skill switch
        {
            "shield" => TryShield(state),
            "ring" => TryRing(state),
            _ => TryBomb(state),
        };

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

    private bool TryRing(GameState state)
    {
        if (_cdRing > 0) return false;
        var rand = state.GetRandom().Random;
        _cdRing = RingCooldown;
        _ringActive = true;
        _ringExpanding = true;
        _ringReturn = rand.NextDouble() < RingReturnChance;
        _ringRadius = RingStartRadius;
        _ringHold = RingHold;
        _ringCount = rand.Next(1, 4);
        _ringGapOffset = rand.NextDouble() * 2 * System.Math.PI;
        _ringCenter = Position;
        _ringMax = MaxRadiusToCover(state.GetCurrentRoom(), Position);
        EmitVisual(state, "attack");
        return true;
    }

    private static int MaxRadiusToCover(Room room, Position center)
    {
        var w = room.Width - 1;
        var h = room.Height - 1;
        var d2 = System.Math.Max(
            System.Math.Max(center.SquaredDistanceTo(new Position(0, 0)), center.SquaredDistanceTo(new Position(w, 0))),
            System.Math.Max(center.SquaredDistanceTo(new Position(0, h)), center.SquaredDistanceTo(new Position(w, h))));
        return (int)System.Math.Ceiling(System.Math.Sqrt(d2));
    }

    private void SpawnRing(GameState state, Position center, int leadRadius)
    {
        for (var i = 0; i < _ringCount; i++)
        {
            var radius = leadRadius - i * RingSpacing;
            if (radius >= 0 && radius <= _ringMax)
                SpawnSingleRing(state, center, radius);
        }
    }

    private void SpawnSingleRing(GameState state, Position center, int radius)
    {
        var room = state.GetCurrentRoom();
        var player = state.Player.Position;
        for (var dx = -radius; dx <= radius; dx++)
        for (var dy = -radius; dy <= radius; dy++)
        {
            var dist = System.Math.Sqrt(dx * dx + dy * dy);
            if (dist < radius - 0.5 || dist >= radius + 0.5) continue;
            if (IsHole(dx, dy, radius)) continue;
            var p = new Position(center.X + dx, center.Y + dy);
            if (!room.IsInside(p) || !room.IsWalkable(p)) continue;
            state.AddProjectile(new NeironkaRing(p));
            if (p == player) state.Player.TakeDamage(RingDamage, state);
        }
    }

    private bool IsHole(int dx, int dy, int radius)
    {
        if (radius <= 1) return false;
        var angle = System.Math.Atan2(dy, dx);
        var sector = 2 * System.Math.PI / RingGaps;
        var halfWidth = RingHoleTilesPerSide / System.Math.Max(radius, 1);
        for (var k = 0; k < RingGaps; k++)
        {
            var center = _ringGapOffset + k * sector;
            if (System.Math.Abs(NormalizeAngle(angle - center)) <= halfWidth) return true;
        }
        return false;
    }

    private static double NormalizeAngle(double a)
    {
        while (a > System.Math.PI) a -= 2 * System.Math.PI;
        while (a < -System.Math.PI) a += 2 * System.Math.PI;
        return a;
    }

    private static string Classify(string? reply)
    {
        var t = (reply ?? "").ToLowerInvariant();
        if (t.Contains("shield") || t.Contains("defend")) return "shield";
        if (t.Contains("ring") || t.Contains("nova") || t.Contains("circle")) return "ring";
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
               $"ring ready: {(_cdRing == 0 ? "yes" : "no")}. " +
               $"Pick a skill (attack, shield or ring); add 'teleport' to also blink for free.";
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
        var hero = state.Player.Position;
        var maxD2 = TeleportRadius * TeleportRadius;

        if (mode == "random")
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                var p = new Position(rand.Next(room.Width), rand.Next(room.Height));
                if (state.CanMoveTo(p) && p.SquaredDistanceTo(hero) <= maxD2) { BlinkTo(state, p); return; }
            }
            return;
        }

        Position? best = null;
        var bestScore = mode == "away" ? int.MinValue : int.MaxValue;
        for (var x = 0; x < room.Width; x++)
        for (var y = 0; y < room.Height; y++)
        {
            var p = new Position(x, y);
            if (!state.CanMoveTo(p)) continue;
            var d = p.SquaredDistanceTo(hero);
            if (d > maxD2) continue;
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
