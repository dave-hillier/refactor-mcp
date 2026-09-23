namespace Shop
{
    public class Report
    {
        public string Print()
        {
            var /*^*/writer = new FileWriter();
            return Describe(writer);
        }

        private static string Describe(FileWriter writer) => "file";

        private static string Describe(IWriter writer) => "any";
    }
}
