namespace Shop
{
    public class Employee
    {
        // Everyone has a name.
        public string Name;

        /// <summary>Where a salesman sells.</summary>
        public string Territory;

        // Pay is monthly.
        public decimal Salary;
    }

    public class Salesman : Employee
    {
        // Sales so far this year.
        public decimal Sales;

        public string Region() => Territory.ToUpperInvariant();
    }
}
