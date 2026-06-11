using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Mole enemy sometimes pops in a random location, after 5 ticks shoots in all for directions
/// </summary>
public class Mole(Position position) : Entities.Mob(EntityType.Mole, position, 5)
{
    
}