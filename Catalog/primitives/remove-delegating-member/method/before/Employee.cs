namespace Staff
{
    public class Employee : Person
    {
        public decimal Salary { get; set; }

        public new string LastName()
        {
            return base.LastName();
        }

        public string Badge() => "Employee " + Name;
    }
}
