using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class ProjectileBehaviourBuilder(Direction direction, IEnumerable<ItemType> modifiers)
{
    private readonly List<ItemType> _items = modifiers.ToList();
    
    public IBehaviour<Projectile> Build()
    {
        return new CompositeBehaviour<Projectile>([
            new DestroyIfInBlockBehaviour<Projectile>(),
            new StraightLineBehaviour<Projectile>(GetSpeed(), direction),
            new FollowEntityBehaviour<Projectile>(GetSpeed(), GetFollowRange() * GetSpeed()),
            new DamageEntityOnCollisionBehaviour(GetDamage()),
            new TimedDieBehaviour(GetTtl()),
            new ChanceBehaviour<Projectile>(new HitPlayerWithLightningBehaviour<Projectile>(), GetSelfLightningChance()),
            new ChanceBehaviour<Projectile>(new SpawnLightningOnCollisionBehaviour<Projectile>(), GetLightningChance()),
            new ChanceBehaviour<Projectile>(new StunEnemyBehaviour(5), GetStunChance())
        ]);
    }
    // TODO:
    // Roc,
    // Haskell,
    // JavaScript,
    // TypeScript,
    // Go,

    private float GetStunChance()
    {
        var stunChance = 3f;
        foreach (var item in _items)
        {
            if (item == ItemType.Python3) stunChance += 0.05f;
            if (item == ItemType.Haskell) stunChance += 0.025f;
            if (item == ItemType.JavaScript) stunChance += 0.025f;
            if (item == ItemType.TypeScript) stunChance += 0.025f;
        }
        return stunChance;
    }

    private int GetDamage()
    {
        return 1;
    }

    private int GetTtl()
    {
        return 3;
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
            if (item == ItemType.Zig) chance += 0.05f;
            if (item == ItemType.Cpp) chance += 0.01f;
            if (item == ItemType.Rust) chance += 0.005f;
            if (item == ItemType.Asm) chance += 0.005f;
            if (item == ItemType.AnsiC) chance += 0.01f;
        }
        return chance;
    }
    
    private double GetSelfLightningChance()
    {
        var chance = 0f;
        foreach (var item in _items)
        {
            if (item == ItemType.Cpp) chance += 0.005f;
        }
        return chance;
    }

    private int GetFollowRange()
    {
        var range = 0;
        foreach (var item in _items)
        {
            if (item == ItemType.OCaml) range += 2;
            if (item == ItemType.Scala3) range += 1;
        }
        return range;
    }
}