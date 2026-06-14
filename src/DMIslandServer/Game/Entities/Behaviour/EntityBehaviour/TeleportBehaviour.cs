namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class TeleportBehaviour : IBehaviour<Entity>
{
    public void PerformTurn(Entity self, GameState state)
    {
        var point = CommonAlgorithms.ChooseNewPoint(self.Position, state);
        self.TryMoveTo(point);
    }
}