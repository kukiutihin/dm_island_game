using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities;

public abstract class Projectile(EntityType name, Position position, int maxHp) : Entity(name, position, maxHp), IActor
{
    public override bool IsBlocking => false;
    private bool _deadOnNextMove;

    public virtual void PerformTurn(GameState state)
    {
        if (_deadOnNextMove)
            Kill(state);
        PreviousPosition = Position;
    }

    protected override void OnDeath(GameState state) { }

    public void KillOnNextMove(GameState state)
    {
        _deadOnNextMove = true;
    }
}