namespace Shop
{
    public class Grid
    {
        private readonly int[,] _cells = new int[2, 2];

        public int Corner() => _cells[0, 0];
    }
}
