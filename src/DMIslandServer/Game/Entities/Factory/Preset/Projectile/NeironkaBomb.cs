using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

public class NeironkaBomb(Position position, int fuse) : Entities.Projectile(EntityType.NeironkaBomb, position, int.MaxValue)
{
    private int _fuse = fuse;

    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        if (_fuse-- > 0) return;

        if (state.Player.Position == Position)
            state.Player.TakeDamage(2, state);

        state.AddEvent(new Event(EventType.NeironkaBoom, Position, Id.ToString()));
        Kill(state);
    }

    protected override void OnDamage(int damage, GameState state) { }
}
