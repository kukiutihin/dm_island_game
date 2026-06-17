using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

public class Neironka(Position position) : Entities.Mob(EntityType.Neironka, position, BaseHp)
{
    private const int BaseHp = 36;

    private const int ShieldPeriod = 20;
    private const int ShieldDuration = 4;
    private const int ShieldHealPerTurn = 2;
    private const int ShieldBlastRadius = 2;
    private const int ShieldBlastDamage = 2;
    private const int BurstPeriod = 17;
    private const int TriplePeriod = 35;
    private const int TriplePose = 3;
    private const int BurstPose = 1;
    private const int MinFuse = 2;
    private const int MaxFuse = 5;
    private const double TeleportChance = 0.5;

    private int _cdShield = ShieldPeriod;
    private int _cdBurst = BurstPeriod;
    private int _cdTriple = TriplePeriod;

    private int _hideTurns;
    private int _poseTurns;
    private bool _poseIsTriple;
    private int _pendingBursts;

    private bool Shielded => _hideTurns > 0;

    public override void TakeDamage(int amount, GameState state)
    {
        if (Shielded) return;
        base.TakeDamage(amount, state);
    }

    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);

        if (_cdShield > 0) _cdShield--;
        if (_cdBurst > 0) _cdBurst--;
        if (_cdTriple > 0) _cdTriple--;

        if (_hideTurns > 0)
        {
            if (Hp < MaxHp) Heal(System.Math.Min(ShieldHealPerTurn, MaxHp - Hp));
            if (--_hideTurns == 0) FinishAbility(state);
            return;
        }

        if (_pendingBursts > 0)
        {
            CarpetBomb(state);
            if (--_pendingBursts == 0) FinishAbility(state);
            return;
        }

        if (_poseTurns > 0)
        {
            if (--_poseTurns == 0) _pendingBursts = _poseIsTriple ? 3 : 1;
            return;
        }

        if (_cdTriple <= 0 && !BombsLive(state))
        {
            _cdTriple = TriplePeriod;
            _cdBurst = BurstPeriod;
            _poseIsTriple = true;
            _poseTurns = TriplePose;
            EmitVisual(state, "attack");
            return;
        }

        if (_cdShield <= 0)
        {
            _cdShield = ShieldPeriod;
            _hideTurns = ShieldDuration;
            if (Position.SquaredDistanceTo(state.Player.Position) <= ShieldBlastRadius * ShieldBlastRadius)
                state.Player.TakeDamage(ShieldBlastDamage, state);
            EmitVisual(state, "hide");
            return;
        }

        if (_cdBurst <= 0 && !BombsLive(state))
        {
            _cdBurst = BurstPeriod;
            _poseIsTriple = false;
            _poseTurns = BurstPose;
            EmitVisual(state, "attack");
        }
    }

    private static bool BombsLive(GameState state) =>
        state.Projectiles.Any(p => p.IsAlive && p.Type == EntityType.NeironkaBomb)
        || state.DelayedProjectiles.Any(p => p.IsAlive && p.Type == EntityType.NeironkaBomb);

    private void FinishAbility(GameState state)
    {
        EmitVisual(state, "idle");
        if (state.GetRandom().Random.NextDouble() < TeleportChance)
            Teleport(state);
    }

    private void Teleport(GameState state)
    {
        var room = state.GetCurrentRoom();
        var rand = state.GetRandom().Random;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var p = new Position(rand.Next(room.Width), rand.Next(room.Height));
            if (!state.CanMoveTo(p)) continue;
            Position = p;
            PreviousPosition = p;
            state.AddEvent(new Event(EventType.NeironkaBoom, p, Id.ToString()));
            return;
        }
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
        state.AddEvent(new Event(EventType.NeironkaVisual, Position, $"{Id}|{kind}"));
    }
}
