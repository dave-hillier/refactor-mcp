namespace Staff
{
    public class Employee : Person
    {
        public decimal Salary { get; set; }

        public string Badge() => Greeting("Employee") + " (" + LastName() + ")";
    }
}
