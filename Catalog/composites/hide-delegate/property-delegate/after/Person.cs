namespace Staff
{
    public class Person
    {
        public string Name { get; set; } = "";

        public Department Department { get; set; } = new Department();

        public Employee Manager => Department.Manager;
    }
}
