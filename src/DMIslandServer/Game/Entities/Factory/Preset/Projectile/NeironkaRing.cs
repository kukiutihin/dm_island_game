using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

public class NeironkaRing(Position position) : Entities.Projectile(EntityType.NeironkaBomb, position, int.MaxValue)
{
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        Kill(state);
    }

    protected override void OnDamage(int damage, GameState state) { }
}
