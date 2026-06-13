namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class TimedDieBehaviour(int ttl) : IBehaviour<Projectile>
{
    private int _ttl = ttl;
    
    public void PerformTurn(Projectile self, GameState state)
    {
        if (_ttl-- <= 0) self.KillOnNextMove(state);
    }
}