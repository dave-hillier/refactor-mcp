namespace Shop
{
    public abstract class Employee
    {
        public string Name;

        public string QuotaReport()
        {
            return Name + " has a quota";
        }
    }
}
