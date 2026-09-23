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
        public new string Name { get => base.Name; set => base.Name = value; }

        public string Title() => "Manager " + base.Name;
    }
}
