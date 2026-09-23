namespace Shop
{
    public class Gauge
    {
        public string Describe(double ratio, int? count)
        {
            if (!(ratio > 0.5) || !(count < 3))
            {
                return "low";
            }
            else
            {
                return "high";
            }
        }
    }
}
