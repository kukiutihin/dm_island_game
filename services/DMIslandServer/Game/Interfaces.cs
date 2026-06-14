namespace RoguelikeServerMVP.Game;

/// <summary>
/// Любой «актер», у которого есть ход: игрок, моб и т.п.
/// </summary>
public interface IActor
{
    void PerformTurn(GameState state);
}
