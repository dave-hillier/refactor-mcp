namespace Staff
{
    public class Employee : Person
    {
        // The person this employee is.
        private readonly Person _person = new Person { Name = "Ann" };

        public decimal Salary { get; set; }

        public string Badge() => _person.Greeting("Employee") + " (" + _person.Name + ")";
    }
}
