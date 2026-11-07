namespace DDCore.Core.Geometry;

public abstract record AoeShape
{
    public double SignedDistance(Coordinate2D point, Coordinate2D origin, double rotation = 0)
    {
        var local = point.ToLocal(origin, rotation);
        var result = SignedDistanceLocal(local);
        if (!double.IsFinite(result))
        {
            throw new InvalidOperationException("Shape calculation produced a non-finite result.");
        }

        return result;
    }

    public bool Contains(Coordinate2D point, Coordinate2D origin, double rotation = 0, double tolerance = 1e-9)
    {
        Coordinate2D.EnsureFinite(tolerance, nameof(tolerance));
        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance cannot be negative.");
        }

        return SignedDistance(point, origin, rotation) <= tolerance;
    }

    protected abstract double SignedDistanceLocal(Coordinate2D point);

    protected static double BoxSignedDistance(Coordinate2D point, double halfExtentX, double halfExtentY)
    {
        var deltaX = Math.Abs(point.X) - halfExtentX;
        var deltaY = Math.Abs(point.Y) - halfExtentY;
        var outside = Math.Sqrt((Math.Max(deltaX, 0) * Math.Max(deltaX, 0)) + (Math.Max(deltaY, 0) * Math.Max(deltaY, 0)));
        return outside + Math.Min(Math.Max(deltaX, deltaY), 0);
    }

    protected static double RequirePositive(double value, string name)
    {
        Coordinate2D.EnsureFinite(value, name);
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Value must be greater than zero.");
        }

        return value;
    }
}

public sealed record CircleAoe : AoeShape
{
    public CircleAoe(double radius) => Radius = RequirePositive(radius, nameof(radius));

    public double Radius { get; }

    protected override double SignedDistanceLocal(Coordinate2D point) => point.Length - Radius;
}

public sealed record ConeAoe : AoeShape
{
    public ConeAoe(double radius, double halfAngle)
    {
        Radius = RequirePositive(radius, nameof(radius));
        Coordinate2D.EnsureFinite(halfAngle, nameof(halfAngle));
        if (halfAngle <= 0 || halfAngle > Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(halfAngle), "Half-angle must be in (0, pi].");
        }

        HalfAngle = halfAngle;
    }

    public double Radius { get; }

    public double HalfAngle { get; }

    protected override double SignedDistanceLocal(Coordinate2D point) => SectorSignedDistance(point, Radius, HalfAngle);

    internal static double SectorSignedDistance(Coordinate2D point, double radius, double halfAngle)
    {
        var distance = point.Length;
        if (distance == 0)
        {
            return 0;
        }

        var angle = Math.Abs(Math.Atan2(point.Y, point.X));
        if (angle <= halfAngle)
        {
            if (distance >= radius)
            {
                return distance - radius;
            }

            var sideDistance = distance * Math.Sin(halfAngle - angle);
            return -Math.Min(radius - distance, sideDistance);
        }

        var angleDelta = angle - halfAngle;
        var projected = distance * Math.Cos(angleDelta);
        if (projected <= 0)
        {
            return distance;
        }

        if (projected <= radius)
        {
            return distance * Math.Sin(angleDelta);
        }

        var endpoint = new Coordinate2D(radius * Math.Cos(halfAngle), Math.CopySign(radius * Math.Sin(halfAngle), point.Y));
        return Coordinate2D.Distance(point, endpoint);
    }
}

public sealed record RectangleAoe : AoeShape
{
    public RectangleAoe(double length, double halfWidth)
    {
        Length = RequirePositive(length, nameof(length));
        HalfWidth = RequirePositive(halfWidth, nameof(halfWidth));
    }

    public double Length { get; }

    public double HalfWidth { get; }

    protected override double SignedDistanceLocal(Coordinate2D point) =>
        BoxSignedDistance(new Coordinate2D(point.X - (Length / 2), point.Y), Length / 2, HalfWidth);
}

public sealed record DonutAoe : AoeShape
{
    public DonutAoe(double innerRadius, double outerRadius)
    {
        InnerRadius = RequirePositive(innerRadius, nameof(innerRadius));
        OuterRadius = RequirePositive(outerRadius, nameof(outerRadius));
        if (innerRadius >= outerRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be less than outer radius.");
        }
    }

    public double InnerRadius { get; }

    public double OuterRadius { get; }

    protected override double SignedDistanceLocal(Coordinate2D point)
    {
        var distance = point.Length;
        return Math.Max(InnerRadius - distance, distance - OuterRadius);
    }
}

public sealed record DonutSectorAoe : AoeShape
{
    public DonutSectorAoe(double innerRadius, double outerRadius, double halfAngle)
    {
        InnerRadius = RequirePositive(innerRadius, nameof(innerRadius));
        OuterRadius = RequirePositive(outerRadius, nameof(outerRadius));
        if (innerRadius >= outerRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be less than outer radius.");
        }

        Coordinate2D.EnsureFinite(halfAngle, nameof(halfAngle));
        if (halfAngle <= 0 || halfAngle > Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(halfAngle), "Half-angle must be in (0, pi].");
        }

        HalfAngle = halfAngle;
    }

    public double InnerRadius { get; }

    public double OuterRadius { get; }

    public double HalfAngle { get; }

    protected override double SignedDistanceLocal(Coordinate2D point) =>
        Math.Max(ConeAoe.SectorSignedDistance(point, OuterRadius, HalfAngle), InnerRadius - point.Length);
}

public sealed record CrossAoe : AoeShape
{
    public CrossAoe(double armLength, double halfWidth)
    {
        ArmLength = RequirePositive(armLength, nameof(armLength));
        HalfWidth = RequirePositive(halfWidth, nameof(halfWidth));
        if (halfWidth >= armLength)
        {
            throw new ArgumentOutOfRangeException(nameof(halfWidth), "Half-width must be less than arm length.");
        }
    }

    public double ArmLength { get; }

    public double HalfWidth { get; }

    protected override double SignedDistanceLocal(Coordinate2D point)
    {
        var horizontal = BoxSignedDistance(point, ArmLength, HalfWidth);
        var vertical = BoxSignedDistance(point, HalfWidth, ArmLength);
        return Math.Min(horizontal, vertical);
    }
}
