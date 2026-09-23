namespace Geometry
{
    public record Limits(int Min);

    public class Grid
    {
        public int Clamp(int value, int max)
        {
            if (value > max)
                value = max;
            return value;
        }

        public int Sample()
        {
            return Clamp(12, 10);
        }
    }
}
