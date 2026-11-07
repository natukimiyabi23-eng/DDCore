namespace DDCore.Core.Geometry;

public readonly record struct Coordinate2D(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    public double Length => Math.Sqrt((X * X) + (Y * Y));

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
}
