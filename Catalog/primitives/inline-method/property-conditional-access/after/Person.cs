namespace Company
{
    public class Department
    {
        public Employee Manager { get; set; }
    }

    public class Employee
    {
        public string Name { get; set; }
    }

    public class Person
    {
        public Department Department { get; set; }
    }
}
