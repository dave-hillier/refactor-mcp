namespace Maths
{
    public class Sign
    {
        public string Describe(int value)
        {
            if (value < 0)
                return "negative";

            return value == 0 ? "zero" : "positive";
        }
    }
}
