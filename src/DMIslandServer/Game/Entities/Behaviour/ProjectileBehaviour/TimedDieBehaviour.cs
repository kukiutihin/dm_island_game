namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class TimedDieBehaviour(int ttl) : IBehaviour
{
    private int _ttl = ttl;
    
    public void PerformTurn(Entity self, GameState state)
    {
        if (_ttl-- < 0) self.Kill(state);
    }
}