namespace Shop
{
    public class Report
    {
        public string Describe()
        {
            string[] /*^*/row = new string[2];
            row[0] = "Liverpool";
            row[1] = "15";
            return row[0] + ": " + row[1];
        }
    }
}
