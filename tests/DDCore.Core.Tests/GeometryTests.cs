using DDCore.Core.Geometry;

namespace DDCore.Core.Tests;

public sealed class GeometryTests
{
    private static readonly Coordinate2D Origin = new(0, 0);

    [Fact]
    public void CoordinateRotationAndTranslationAreDeterministic()
    {
        var transformed = new Coordinate2D(2, 0).Rotate(Math.PI / 2).Translate(new Coordinate2D(5, -2));

        Assert.Equal(5, transformed.X, 10);
        Assert.Equal(0, transformed.Y, 10);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5, 0, true)]
    [InlineData(5.001, 0, false)]
    public void CircleHandlesInsideBoundaryAndOutside(double x, double y, bool expected) =>
        Assert.Equal(expected, new CircleAoe(5).Contains(new(x, y), Origin));

    [Fact]
    public void TranslationDoesNotChangeCircleDistance()
    {
        var shape = new CircleAoe(3);
        Assert.Equal(shape.SignedDistance(new(2, 0), Origin), shape.SignedDistance(new(12, -4), new(10, -4)), 10);
    }

    [Theory]
    [InlineData(4, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(0, 4, false)]
    [InlineData(11, 0, false)]
    public void ConeHandlesAngularAndRadialBounds(double x, double y, bool expected) =>
        Assert.Equal(expected, new ConeAoe(10, Math.PI / 4).Contains(new(x, y), Origin));

    [Fact]
    public void ConeRotationIsAppliedAroundOrigin()
    {
        var shape = new ConeAoe(10, Math.PI / 6);
        Assert.True(shape.Contains(new Coordinate2D(0, 5), Origin, Math.PI / 2));
        Assert.False(shape.Contains(new Coordinate2D(5, 0), Origin, Math.PI / 2));
    }

    [Fact]
    public void PiHalfAngleBehavesAsFullCircle()
    {
        var shape = new ConeAoe(5, Math.PI);
        Assert.True(shape.Contains(new Coordinate2D(-4, 0), Origin));
        Assert.False(shape.Contains(new Coordinate2D(-6, 0), Origin));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(10, 2, true)]
    [InlineData(-0.01, 0, false)]
    [InlineData(5, 2.01, false)]
    public void RectangleUsesForwardLengthAndHalfWidth(double x, double y, bool expected) =>
        Assert.Equal(expected, new RectangleAoe(10, 2).Contains(new(x, y), Origin));

    [Theory]
    [InlineData(2.99, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(5, 0, true)]
    [InlineData(7, 0, true)]
    [InlineData(7.01, 0, false)]
    public void DonutHandlesBothRadialBoundaries(double x, double y, bool expected) =>
        Assert.Equal(expected, new DonutAoe(3, 7).Contains(new(x, y), Origin));

    [Theory]
    [InlineData(5, 0, true)]
    [InlineData(2, 0, false)]
    [InlineData(8, 0, false)]
    [InlineData(0, 5, false)]
    public void DonutSectorCombinesRadialAndAngularBounds(double x, double y, bool expected) =>
        Assert.Equal(expected, new DonutSectorAoe(3, 7, Math.PI / 4).Contains(new(x, y), Origin));

    [Theory]
    [InlineData(5, 0, true)]
    [InlineData(0, 5, true)]
    [InlineData(1, 1, true)]
    [InlineData(3, 3, false)]
    [InlineData(7, 0, false)]
    public void CrossIsUnionOfPerpendicularArms(double x, double y, bool expected) =>
        Assert.Equal(expected, new CrossAoe(6, 1).Contains(new(x, y), Origin));

    [Fact]
    public void CrossRotationAndTranslationAreApplied()
    {
        var shape = new CrossAoe(6, 1);
        Assert.True(shape.Contains(new Coordinate2D(13, 13), new Coordinate2D(10, 10), Math.PI / 4));
        Assert.False(shape.Contains(new Coordinate2D(13, 10), new Coordinate2D(10, 10), Math.PI / 4));
    }

    [Fact]
    public void SignedDistanceIsNegativeInsideZeroAtBoundaryAndPositiveOutside()
    {
        var shape = new CircleAoe(5);
        Assert.True(shape.SignedDistance(new(0, 0), Origin) < 0);
        Assert.Equal(0, shape.SignedDistance(new(5, 0), Origin), 10);
        Assert.True(shape.SignedDistance(new(6, 0), Origin) > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidRadiusIsRejected(double radius) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleAoe(radius));

    [Fact]
    public void InvalidCoordinatesAndRotationAreRejected()
    {
        var shape = new CircleAoe(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => shape.Contains(new(double.NaN, 0), Origin));
        Assert.Throws<ArgumentOutOfRangeException>(() => shape.Contains(Origin, Origin, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => shape.Contains(Origin, Origin, tolerance: -1));
    }
}
