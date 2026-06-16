using RoguelikeServerMVP.Game;

namespace GameTests;

public class PrimitiveTypesTests
{
    [Theory]
    [InlineData(Direction.Up, 5, 4)]
    [InlineData(Direction.Down, 5, 6)]
    [InlineData(Direction.Left, 4, 5)]
    [InlineData(Direction.Right, 6, 5)]
    public void Position_Move_StepsOneTileInDirection(Direction dir, int ex, int ey)
    {
        Assert.Equal(new Position(ex, ey), new Position(5, 5).Move(dir));
    }

    [Fact]
    public void Position_Equality_And_Operators()
    {
        Assert.True(new Position(1, 2) == new Position(1, 2));
        Assert.True(new Position(1, 2) != new Position(2, 1));
        Assert.Equal(new Position(3, 5), new Position(1, 2) + new Position(2, 3));
        Assert.Equal(new Position(1, 1), new Position(3, 4) - new Position(2, 3));
    }

    [Fact]
    public void Position_SquaredDistanceTo_IsEuclideanSquared()
    {
        Assert.Equal(25, new Position(0, 0).SquaredDistanceTo(new Position(3, 4)));
    }

    [Fact]
    public void Position_CreateRectangle_CoversInclusiveArea()
    {
        var tiles = Position.CreateRectangle(new Position(0, 0), new Position(2, 1)).ToList();
        Assert.Equal(6, tiles.Count);
        Assert.Contains(new Position(0, 0), tiles);
        Assert.Contains(new Position(2, 1), tiles);
    }

    [Theory]
    [InlineData(Direction.Up, Direction.Right)]
    [InlineData(Direction.Right, Direction.Down)]
    [InlineData(Direction.Down, Direction.Left)]
    [InlineData(Direction.Left, Direction.Up)]
    public void DirectionUtil_TurnLeft_RotatesCounterClockwise(Direction input, Direction expected)
    {
        Assert.Equal(expected, DirectionUtil.TurnLeft(input));
    }

    [Fact]
    public void DirectionUtil_TurnRight_IsInverseOfTurnLeft()
    {
        foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            Assert.Equal(d, DirectionUtil.TurnLeft(DirectionUtil.TurnRight(d)));
    }

    [Fact]
    public void DirectionUtil_Flip_IsOpposite()
    {
        Assert.Equal(Direction.Down, DirectionUtil.Flip(Direction.Up));
        Assert.Equal(Direction.Right, DirectionUtil.Flip(Direction.Left));
    }
}
