namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class RandomWalkBehaviour(int waitingTime, int stepChance) : IBehaviour
{
    private State _state = State.Waiting;
    private Position _target;
    
    public void PerformTurn(Entity self, GameState state)
    {
        if (!self.IsAlive)
            return;
        
        Console.WriteLine($"RandomWalkBehaviour st:{_state} targ:{_target}");
        
        if (_state == State.Waiting && state.GetRandom().OneIn(waitingTime))
        {
            _state = State.GoingToAPoint;
            _target = ChooseNewPoint(_target, state);
        }

        if (_state == State.GoingToAPoint && self.Position == _target)
        {
            _state = State.Waiting;
        }
        
        if (_state == State.GoingToAPoint && self.Position != _target)
        {
            var nextStep = CommonAlgorithms.Pathfind(self, state, _target);
            
            if (nextStep.HasValue) self.TryMoveTo(nextStep.Value);
            else _target = ChooseNewPoint(_target, state);
        }
    }

    private static Position ChooseNewPoint(Position fallback, GameState state)
    {
        var width = state.GetCurrentRoom().Width - 1;
        var height = state.GetCurrentRoom().Height - 1;
        for (var i = 0; i < 1000; i++)
        {
            var position = state.GetRandom().RandomPosition(width, height);
            if (state.CanMoveTo(position))
                return position;
        }
        return fallback;
    }
    
    private enum State
    {
        GoingToAPoint,
        Waiting,
    }
}