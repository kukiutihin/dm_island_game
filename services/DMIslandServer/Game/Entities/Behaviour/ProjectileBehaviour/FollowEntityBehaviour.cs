namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class FollowEntityBehaviour(int speed, int range) : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        var target = state.Mobs.MinBy(x => x.Position.SquaredDistanceTo(self.Position));
        
        if (target == null || target.Position.SquaredDistanceTo(self.PreviousPosition) > range)
            return;
        
        // Cancel previous movement attemts:
        self.TryMoveTo(self.PreviousPosition);

        for (var i = 0; i < speed; i++)
        {
            var targetPos = CommonAlgorithms.Pathfind(self, state, target.Position);
            if (targetPos.HasValue) self.TryMoveTo(targetPos.GetValueOrDefault());
        }
    }
}