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
        public string Title() => "Manager " + Name;
    }
}
