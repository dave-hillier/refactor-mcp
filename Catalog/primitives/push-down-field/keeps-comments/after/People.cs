namespace Shop
{
    public class Employee
    {
        // Everyone has a name.
        public string Name;

        // Pay is monthly.
        public decimal Salary;
    }

    public class Salesman : Employee
    {
        // Sales so far this year.
        public decimal Sales;
        /// <summary>Where a salesman sells.</summary>
        public string Territory;

        public string Region() => Territory.ToUpperInvariant();
    }
}
