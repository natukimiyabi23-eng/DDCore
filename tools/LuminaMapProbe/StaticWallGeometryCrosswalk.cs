public static class StaticWallGeometryCrosswalk
{
    public static double DistanceToNearestSupportingLine(
        double x,
        double z,
        string direction,
        double minX,
        double maxX,
        double minZ,
        double maxZ)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z)
            || !double.IsFinite(minX) || !double.IsFinite(maxX)
            || !double.IsFinite(minZ) || !double.IsFinite(maxZ)
            || minX > maxX || minZ > maxZ)
            throw new ArgumentOutOfRangeException(nameof(minX), "Coordinates and bounds must be finite and ordered.");

        return direction switch
        {
            "North" or "South" => Math.Min(Math.Abs(z - minZ), Math.Abs(z - maxZ)),
            "East" or "West" => Math.Min(Math.Abs(x - minX), Math.Abs(x - maxX)),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), "Only cardinal directions are supported."),
        };
    }
}
