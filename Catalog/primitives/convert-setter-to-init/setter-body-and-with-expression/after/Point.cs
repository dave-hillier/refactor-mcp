namespace Shop
{
    public record Point
    {
        private int _x;

        public int X
        {
            get => _x;
            init => _x = value < 0 ? 0 : value;
        }

        public Point Moved(int x) => this with { X = x };
    }
}
