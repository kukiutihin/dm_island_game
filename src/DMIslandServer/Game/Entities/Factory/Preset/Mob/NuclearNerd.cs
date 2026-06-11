using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Nuclear nerd: runs away from player and sometimes spawns missiles
/// </summary>
public class NuclearNerd(Position position) : Entities.Mob(EntityType.NuclearNerd, position, 10)
{
    
}