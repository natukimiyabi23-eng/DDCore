using Xunit;

public sealed class StaticWallGeometryCrosswalkTests
{
    [Theory]
    [InlineData("North", 100, 12, 2)]
    [InlineData("South", -100, -13, 3)]
    [InlineData("East", 12, 100, 2)]
    [InlineData("West", -13, -100, 3)]
    public void UsesNearestOrientationMatchedSupportingLine(string direction, double x, double z, double expected)
    {
        var distance = StaticWallGeometryCrosswalk.DistanceToNearestSupportingLine(
            x,
            z,
            direction,
            -10,
            10,
            -10,
            10);

        Assert.Equal(expected, distance);
    }

    [Fact]
    public void RejectsNonFiniteMeasurements()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticWallGeometryCrosswalk.DistanceToNearestSupportingLine(
            double.NaN,
            0,
            "North",
            -1,
            1,
            -1,
            1));
    }

    [Fact]
    public void RejectsReversedBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticWallGeometryCrosswalk.DistanceToNearestSupportingLine(
            0,
            0,
            "North",
            2,
            1,
            -1,
            1));
    }

    [Fact]
    public void RejectsUnknownDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StaticWallGeometryCrosswalk.DistanceToNearestSupportingLine(
            0,
            0,
            "Diagonal",
            -1,
            1,
            -1,
            1));
    }
}
