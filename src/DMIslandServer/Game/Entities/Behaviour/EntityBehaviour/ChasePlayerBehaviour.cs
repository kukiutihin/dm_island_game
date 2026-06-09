namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ChasePlayerBehaviour(int skipMoves) : IBehaviour
{
    private int _step = skipMoves;
    
    public void PerformTurn(Entity self, GameState state)
    {
        var neighbours = CommonAlgorithms
            .GetNeighbours(self.Position)
            .Where(state.CanMoveTo)
            .ToList();
            
        if (neighbours.Count == 0)
            return;
        
        var position = neighbours.MinBy(x => state.Player.Position.SquaredDistanceTo(x));

        if (_step-- > 0)
            return;
        
        self.TryMoveTo(position);
        _step = skipMoves;
    }
}