namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public static class CommonAlgorithms
{
    public static IEnumerable<Position> GetNeighbours(Position position)
    {
        return [position.Move(Direction.Down), position.Move(Direction.Up), position.Move(Direction.Left), position.Move(Direction.Right)];
    }

    private static Position GetFirstStep(Dictionary<Position, Position> parents, Position curr)
    {
        return parents.TryGetValue(curr, out var parent) ? GetFirstStep(parents, parent) : curr;
    }
    
    public static Position? Pathfind(Entity self, GameState state, Position target)
    {
        var q = new PriorityQueue<Position, int>();
        var visited = new HashSet<Position>();
        var parents = new Dictionary<Position, Position>();
        q.Enqueue(self.Position, 0);
        
        while (q.Count > 0)
        {
            var position = q.Dequeue();
            var neighbors = GetNeighbours(position)
                .Where(visited.Add)
                .Where(x => state.CanMoveTo(x) || x == target)
                .ToArray();
            
            foreach (var neighbor in neighbors)
            {
                if (position != self.Position)
                    parents[neighbor] = position;
                
                if (position == target)
                    return GetFirstStep(parents, position);
                
                var distance = target.SquaredDistanceTo(neighbor);
                q.Enqueue(neighbor, distance);
            }
        }
        
        Console.WriteLine("Failed every move");

        return null;
    }
    
    public static Position ChooseNewPoint(Position fallback, GameState state)
    {
        var width = state.GetCurrentRoom().Width - 1;
        var height = state.GetCurrentRoom().Height - 1;
        for (var i = 0; i < 1000; i++)
        {
            var position = state.GetRandom().RandomPosition(width, height);
            if (state.CanMoveTo(position))
                return position;
        }
        return fallback;
    }
}