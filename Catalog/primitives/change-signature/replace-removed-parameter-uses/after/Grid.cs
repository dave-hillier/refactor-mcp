using System;

namespace Geometry
{
    public record Point(int X, int Y);

    public class Grid
    {
        public double DistanceFromOrigin(Point point)
        {
            return Math.Sqrt(point.X * point.X + point.Y * point.Y);
        }

        public double Sample()
        {
            return DistanceFromOrigin(new Point(3, 4));
        }
    }
}
