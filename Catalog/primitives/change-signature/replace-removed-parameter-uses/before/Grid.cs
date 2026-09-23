using System;

namespace Geometry
{
    public record Point(int X, int Y);

    public class Grid
    {
        public double DistanceFromOrigin(int x, int y)
        {
            return Math.Sqrt(x * x + y * y);
        }

        public double Sample()
        {
            return DistanceFromOrigin(3, 4);
        }
    }
}
