namespace Shop
{
    public class Sample
    {
        public string Describe(int value, int limit)
        {
            /*^*/if (value == 0)
            {
                return "zero";
            }
            else if (value == limit)
            {
                return "limit";
            }

            return "other";
        }
    }
}
