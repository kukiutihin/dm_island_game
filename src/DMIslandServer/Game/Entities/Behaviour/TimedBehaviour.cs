namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public class TimedBehaviour<T>(IBehaviour<T> behaviour, int time) : IBehaviour<T> where T : Entity
{
    private int _time = time;
    public void PerformTurn(T self, GameState state)
    {
        if (--_time >= 0)
            return;
        
        _time = time;
        behaviour.PerformTurn(self, state);
    }
}