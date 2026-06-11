using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game;

public abstract class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
    public EntityType Type { get; }
    public Position Position { get; protected set; }
    public Position PreviousPosition { get; protected set; }

    public int MaxHp { get; private set; }
    public int Hp { get; private set; }
    public bool IsAlive => Hp > 0;

    /// <summary>
    /// Можно ли пройти через сущность.
    /// Стены: false, мобы: false, декор может быть true.
    /// </summary>
    public virtual bool IsBlocking => false;

    protected Entity(EntityType name, Position position, int maxHp)
    {
        Type = name;
        Position = position;
        PreviousPosition = position;
        MaxHp = maxHp;
        Hp = maxHp;
    }

    public void TakeDamage(int amount, GameState state)
    {
        if (!IsAlive) return;
        
        Hp = Math.Max(0, Hp - amount);
        OnDamage(amount, state);
        if (Hp <= 0) OnDeath(state);
    }
    
    public void Heal(int amount)
    {
        Hp += amount;
        Hp = Math.Max(0, Hp);
    }

    public void AddHealth(int amount)
    {
        MaxHp += amount;
        Hp += amount;
    }

    public void Kill(GameState state)
    {
        Hp = 0;
        OnDeath(state);
    }

    /// <summary>Restores the entity to full health (used when the player restarts).</summary>
    public void RestoreFullHealth()
    {
        Hp = MaxHp;
    }

    public virtual void TryMoveTo(Position target)
    {
        if (!IsAlive) return;
        Position = target;
    }

    protected abstract void OnDeath(GameState state);
    protected abstract void OnDamage(int damage, GameState state);
    
    public override string ToString()
    {
        return $"{Type}, {Id}, ({Position})";
    }
}
