namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public class TimedBehaviour(IBehaviour behaviour, int time) : IBehaviour
{
    private int _time = time;
    public void PerformTurn(Entity self, GameState state)
    {
        if (--_time >= 0)
            return;
        
        _time = time;
        behaviour.PerformTurn(self, state);
    }
}