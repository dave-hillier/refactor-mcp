namespace Staff
{
    public class Employee : Person
    {
        // What the employee is paid.
        public decimal Salary { get; set; }

        /// <summary>The employee's last name.</summary>
        public new string LastName() => base.LastName();

        // A label for the badge.
        public string Badge() => "Employee " + Name;
    }
}
