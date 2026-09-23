namespace Shop
{
    public class Parser
    {
        public int Area(string width, string height)
        {
            var /*^*/size = new int[2];
            int.TryParse(width, out size[0]);
            int.TryParse(height, out size[1]);
            return size[0] * size[1];
        }
    }
}
