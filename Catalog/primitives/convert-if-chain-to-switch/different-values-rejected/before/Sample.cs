namespace Shop
{
    public class Sample
    {
        public string Describe(int a, int b)
        {
            /*^*/if (a == 1)
            {
                return "a";
            }
            else if (b == 2)
            {
                return "b";
            }

            return "neither";
        }
    }
}
