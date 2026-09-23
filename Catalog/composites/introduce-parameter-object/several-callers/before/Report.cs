namespace Shop
{
    public class Report
    {
        public string Print(string title, int low, int high, bool detailed)
        {
            var range = low + "-" + high;
            return detailed ? title + " " + range + " (" + (high - low) + ")" : title + " " + range;
        }

        public string Summary()
        {
            return Print("Small", 0, 10, false) + Print("Large", 10, 100, detailed: true);
        }

        public string Custom(int from)
        {
            return Print(title: "Custom", low: from, high: from * 2, detailed: false);
        }
    }
}
