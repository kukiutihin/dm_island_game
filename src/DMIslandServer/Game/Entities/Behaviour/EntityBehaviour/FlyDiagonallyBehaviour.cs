namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class FlyDiagonallyBehaviour : IBehaviour<Entity>
{
    private Direction _verticalDir = Direction.Up;
    private Direction _horizontalDir = Direction.Left;
    
    public void PerformTurn(Entity self, GameState state)
    {
        if (!state.CanMoveTo(self.Position.Move(_verticalDir)))
            _verticalDir = DirectionUtil.Flip(_verticalDir);
        
        if (state.CanMoveTo(self.Position.Move(_verticalDir)))
            self.TryMoveTo(self.Position.Move(_verticalDir));


        if (!state.CanMoveTo(self.Position.Move(_horizontalDir)))
            _horizontalDir = DirectionUtil.Flip(_horizontalDir);
        
        if (state.CanMoveTo(self.Position.Move(_horizontalDir)))
            self.TryMoveTo(self.Position.Move(_horizontalDir));
    }
}