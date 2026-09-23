namespace Shop
{
    public class Sample
    {
        public int Clamp(int value)
        {
            /*^*/if (value < 0)
            {
                return 0;
            }

            return value;
        }
    }
}
