using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset;

/// <summary>
/// Player projectile
/// Behaviour depends on items given
/// </summary>
/// <param name="direction"></param>
/// <param name="items"></param>
/// <param name="position"></param>
public class Tear(Direction direction, IEnumerable<ItemType> items, Position position) : Entities.Projectile(EntityType.Tear, position, 1)
{
    private readonly IBehaviour<Entities.Projectile> _behaviour = new ProjectileBehaviourBuilder(direction, items).Build();
    
    public override bool IsBlocking => false;
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        if (!IsAlive)
            return;
        _behaviour.PerformTurn(this, state);
    }

    protected override void OnDeath(GameState state)
    {
        state.AddEvent(new Event(EventType.TearPop, Position, Id.ToString()));
    }

    protected override void OnDamage(int damage, GameState state) { }
}