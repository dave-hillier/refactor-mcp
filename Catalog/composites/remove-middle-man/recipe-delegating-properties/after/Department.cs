namespace Company
{
    public class Department
    {
        public Employee Manager { get; set; }

        public string Code { get; set; }
    }

    public class Employee
    {
        public string Name { get; set; }
    }
}
