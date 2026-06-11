using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Skolem enemy has a lot of health, but does not attack.
/// When player is near he just blocks them
/// </summary>
public class Skolem(Position position) : Entities.Mob(EntityType.Skolem, position, 10)
{
    
}