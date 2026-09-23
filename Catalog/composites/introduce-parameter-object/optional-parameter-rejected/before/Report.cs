namespace Shop
{
    public class Report
    {
        public string Print(int low, int high = 100)
        {
            return low + "-" + high;
        }

        public string Sample()
        {
            return Print(5);
        }
    }
}
