namespace Shop
{
    public class Report
    {
        public string Print(string title, Band band, bool detailed)
        {
            var range = band.Low + "-" + band.High;
            return detailed ? title + " " + range + " (" + (band.High - band.Low) + ")" : title + " " + range;
        }

        public string Summary()
        {
            return Print("Small", new Band(0, 10), false) + Print("Large", new Band(10, 100), detailed: true);
        }

        public string Custom(int from)
        {
            return Print(title: "Custom", new Band(from, from * 2), detailed: false);
        }
    }

    public sealed record Band(int Low, int High);
}
