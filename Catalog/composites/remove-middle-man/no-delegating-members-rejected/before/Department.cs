namespace Company
{
    public class Department
    {
        public Employee Manager { get; set; }

        public decimal Budget(int year) => year * 1000m;
    }

    public class Employee
    {
        public string Name { get; set; }
    }
}
