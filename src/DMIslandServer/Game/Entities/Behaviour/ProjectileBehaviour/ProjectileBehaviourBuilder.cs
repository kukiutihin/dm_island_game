using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class ProjectileBehaviourBuilder(Direction direction, IEnumerable<ItemType> modifiers)
{
    private readonly List<ItemType> _items = modifiers.ToList();
    
    public IBehaviour Build()
    {
        return new CompositeBehaviour([
            new DestroyIfInBlockBehaviour(),
            new StraightLineBehaviour(GetSpeed(), direction),
            new FollowEntityBehaviour(GetSpeed(), GetFollowRange() * GetSpeed()),
            new DamageEntityOnCollisionBehaviour(GetDamage()),
            new TimedDieBehaviour(GetTtl()),
        ]);
    }
    // TODO:
    // OCaml,
    // Zig,
    // Roc,
    // JavaScript,
    // TypeScript,
    // Go,x
    // Scala3

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
            if (item == ItemType.Asm) speed += 3;
            if (item == ItemType.AnsiC) speed += 1;
            if (item == ItemType.Rust) speed += 1;
        }
        return speed;
    }

    private int GetFollowRange()
    {
        var range = 0;
        foreach (var item in _items)
        {
            if (item == ItemType.OCaml) range += 3;
            if (item == ItemType.Scala3) range += 1;
        }
        return range;
    }
}