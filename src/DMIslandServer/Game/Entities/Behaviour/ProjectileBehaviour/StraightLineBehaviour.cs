namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class StraightLineBehaviour(Direction direction) : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        // Console.WriteLine($"StraightLineBehaviour {self.Position} {direction}");
        self.TryMoveTo(self.Position.Move(direction));
    }
}