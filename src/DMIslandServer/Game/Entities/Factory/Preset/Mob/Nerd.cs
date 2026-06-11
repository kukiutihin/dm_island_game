using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Nerd enemy. Has 2 attacks:
/// 1. Attacks in a diamond pattern with theta hat (dist = 2) (when near player)
/// 2. Attacks in a straight line with theta hat (dist = 5)
/// </summary>
public class Nerd(Position position) : Entities.Mob(EntityType.Nerd, position, 8)
{
    
}