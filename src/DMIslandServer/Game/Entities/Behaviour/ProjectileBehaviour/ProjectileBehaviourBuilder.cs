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
            new ChanceBehaviour(new HitPlayerWithLightningBehaviour(), GetSelfLightningChance()),
            new ChanceBehaviour(new SpawnLightningOnCollisionBehaviour(), GetLightningChance())
        ]);
    }
    // TODO:
    // Roc,
    // JavaScript,
    // TypeScript,
    // Go,

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
            if (item == ItemType.Asm) speed += 2;
            if (item == ItemType.AnsiC) speed += 1;
            if (item == ItemType.Rust) speed += 1;
        }
        return speed;
    }

    private double GetLightningChance()
    {
        var chance = 0f;
        foreach (var item in _items)
        {
            if (item == ItemType.Zig) chance += 0.15f;
            if (item == ItemType.Cpp) chance += 0.05f;
            if (item == ItemType.Rust) chance += 0.025f;
            if (item == ItemType.Asm) chance += 0.025f;
        }
        return chance;
    }
    
    private double GetSelfLightningChance()
    {
        var chance = 0f;
        foreach (var item in _items)
        {
            if (item == ItemType.Cpp) chance += 0.01f;
        }
        return chance;
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