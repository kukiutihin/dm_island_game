namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

// UNUSED
public class SpawnGoreOnDeathBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        Console.WriteLine($"SpawnGoreOnDeathBehaviour: alive:{self.IsAlive}");
        if (self.IsAlive)
            return;
    }
}