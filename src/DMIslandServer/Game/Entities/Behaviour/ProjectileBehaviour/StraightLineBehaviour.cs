namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class StraightLineBehaviour(int speed, Direction direction) : IBehaviour
{

    public void PerformTurn(Entity self, GameState state)
    {
        for (var i = 0; i < speed; i++)
        {
            self.TryMoveTo(self.Position.Move(direction));
        }
    }
}