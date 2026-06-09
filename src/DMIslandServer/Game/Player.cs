using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Mobs.Factory.Preset;

namespace RoguelikeServerMVP.Game;

public class Player(Position startPos, int maxHp, int attackDamage)
    : Entity(EntityType.Player, startPos, maxHp), IActor
{
    private readonly int _attackDamage = attackDamage;

    public void PerformTurn(GameState state)
    {
        // Игрок управляется снаружи — пусто
    }

    public void TryMove(Direction dir, GameState state)
    {
        var targetPos = Position.Move(dir);

        if (!state.CanMoveTo(targetPos))
            return;

        Position = targetPos;
    }

    public void Attack(Direction dir, GameState state)
    {
        var tearEntity = new Tear(dir, Position);
        state.AddProjectile(tearEntity);
    }

    public void SkipTurn()
    {
        // Ничего
    }

    /// <summary>Instantly moves the player to a tile (used when entering a new room).</summary>
    public void Teleport(Position p)
    {
        Position = p;
        PreviousPosition = p;
    }

    protected override void OnDeath(GameState state)
    {
        
    }

    protected override void OnDamage(int damage, GameState state)
    {
        
    }
}
