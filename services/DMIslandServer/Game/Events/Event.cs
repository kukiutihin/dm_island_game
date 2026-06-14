using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Events;

public class Event(EventType type, Position position, string payload)
{
    public EventType Type => type;
    public Position Position => position;
    public string Payload => payload;
}