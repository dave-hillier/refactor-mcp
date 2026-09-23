namespace Shop
{
    public class Report
    {
        public string Describe()
        {
            Performance row = new Performance();
            row.Club = "Liverpool";
            row.Wins = "15";
            return row.Club + ": " + row.Wins;
        }
    }
}
