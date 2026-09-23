namespace Shop
{
    public class Report
    {
        public string Print(int low, int high)
        {
            if (low < 0)
                low = 0;
            return low + "-" + high;
        }

        public string Sample()
        {
            return Print(1, 5);
        }
    }
}
