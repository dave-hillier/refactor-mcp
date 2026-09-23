namespace Shop
{
    public struct Point
    {
        public int X;
        public int Y;

        public int Distance(Point other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            return dx * dx + dy * dy;
        }
    }
}
