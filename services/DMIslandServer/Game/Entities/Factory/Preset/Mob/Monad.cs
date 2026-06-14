using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Monad enemy. Has a single attack:
/// When player is on the same lane, after 1 tick tries to ramp them
/// Roaming mode: cling to walls
/// </summary>
public class Monad(Position position) : Entities.Mob(EntityType.Monad, position, 6)
{
    
}