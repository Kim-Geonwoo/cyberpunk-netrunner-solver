namespace NetrunnerSolver.Core.Imaging;

public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);

    public Box Inflate(double dx, double dy) => new(X - dx, Y - dy, Width + (2 * dx), Height + (2 * dy));

    public Box Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;

    public Box ToLocalDip(double originX, double originY, double scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be positive.");
        }

        return new((X - originX) / scale, (Y - originY) / scale, Width / scale, Height / scale);
    }
}
