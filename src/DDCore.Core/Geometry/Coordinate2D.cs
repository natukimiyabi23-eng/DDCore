namespace DDCore.Core.Geometry;

public readonly record struct Coordinate2D(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    public double Length
    {
        get
        {
            EnsureFinite(this, nameof(Coordinate2D));
            var maximum = Math.Max(Math.Abs(X), Math.Abs(Y));
            if (maximum == 0)
            {
                return 0;
            }

            var minimum = Math.Min(Math.Abs(X), Math.Abs(Y));
            var ratio = minimum / maximum;
            return maximum * Math.Sqrt(1 + (ratio * ratio));
        }
    }

    public Coordinate2D Translate(Coordinate2D offset) => new(X + offset.X, Y + offset.Y);

    public Coordinate2D Rotate(double radians)
    {
        EnsureFinite(radians, nameof(radians));
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return new((X * cosine) - (Y * sine), (X * sine) + (Y * cosine));
    }

    public Coordinate2D ToLocal(Coordinate2D origin, double rotation)
    {
        EnsureFinite(this, nameof(Coordinate2D));
        EnsureFinite(origin, nameof(origin));
        EnsureFinite(rotation, nameof(rotation));
        return new Coordinate2D(X - origin.X, Y - origin.Y).Rotate(-rotation);
    }

    /// <summary>
    /// Converts a clockwise angle measured from positive Y (the FFXIV/BossMod XZ convention) to this core's
    /// counter-clockwise mathematical angle measured from positive X.
    /// </summary>
    public static double FromClockwisePositiveYRotation(double radians)
    {
        EnsureFinite(radians, nameof(radians));
        return NormalizeRadians((Math.PI / 2) - radians);
    }

    public static double Distance(Coordinate2D left, Coordinate2D right)
    {
        EnsureFinite(left, nameof(left));
        EnsureFinite(right, nameof(right));
        return new Coordinate2D(left.X - right.X, left.Y - right.Y).Length;
    }

    internal static void EnsureFinite(Coordinate2D value, string name)
    {
        if (!value.IsFinite)
        {
            throw new ArgumentOutOfRangeException(name, "Coordinates must be finite.");
        }
    }

    internal static void EnsureFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, "Value must be finite.");
        }
    }

    private static double NormalizeRadians(double radians)
    {
        var normalized = radians % Math.Tau;
        if (normalized < -Math.PI)
        {
            normalized += Math.Tau;
        }
        else if (normalized >= Math.PI)
        {
            normalized -= Math.Tau;
        }

        return normalized;
    }
}
