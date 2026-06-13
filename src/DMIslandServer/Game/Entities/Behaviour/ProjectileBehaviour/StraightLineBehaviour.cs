namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class StraightLineBehaviour<T>(int speed, Direction direction) : IBehaviour<T> where T : Entity
{
    public void PerformTurn(T self, GameState state)
    {
        for (var i = 0; i < speed; i++)
        {
            self.TryMoveTo(self.Position.Move(direction));
        }
    }
}