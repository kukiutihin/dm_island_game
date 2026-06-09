namespace RoguelikeServerMVP.Game.Entities.Debuffs;

public class Debuff(DebuffType debuffType, int duration)
{
    public DebuffType DebuffType => debuffType;
    public int Duration = duration;
}