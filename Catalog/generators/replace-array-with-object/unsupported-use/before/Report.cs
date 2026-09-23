namespace Shop
{
    public class Report
    {
        public string Describe()
        {
            string[] /*^*/row = { "Liverpool", "15" };
            var text = row[0];
            return text + row.Length;
        }
    }
}
