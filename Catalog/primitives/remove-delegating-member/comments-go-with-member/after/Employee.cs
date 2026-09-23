namespace Staff
{
    public class Employee : Person
    {
        // What the employee is paid.
        public decimal Salary { get; set; }

        // A label for the badge.
        public string Badge() => "Employee " + Name;
    }
}
