using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class ProjectileBehaviourBuilder(Direction direction, IEnumerable<ItemType> modifiers)
{
    private readonly List<ItemType> _items = modifiers.ToList();
    
    public IBehaviour Build()
    {
        return new CompositeBehaviour([
            new StraightLineBehaviour(GetSpeed(), direction),
            new DamageEntityOnCollisionBehaviour(GetDamage()),
            new TimedDieBehaviour(GetTtl()),
        ]);
    }

    private int GetDamage()
    {
        return 1;
    }

    private int GetTtl()
    {
        var ttl = 5;
        foreach (var item in _items)
        {
            if (item == ItemType.Python3) ttl += 2;
        }
        return ttl;
    }
    
    private int GetSpeed()
    {
        var speed = 1;
        foreach (var item in _items)
        {
            if (item == ItemType.Cpp) speed++;
        }
        return speed;
    }
}