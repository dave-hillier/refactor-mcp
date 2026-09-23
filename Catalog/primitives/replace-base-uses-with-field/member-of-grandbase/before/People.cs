namespace Staff
{
    public class Person
    {
        public string Name { get; set; } = "";
    }

    public class Employee : Person
    {
        public decimal Salary { get; set; }
    }

    public class Manager : Employee
    {
        private readonly Employee _employee = new Employee();

        public string Title() => "Manager " + Name + " on " + Salary;
    }
}
