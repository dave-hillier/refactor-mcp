namespace Company
{
    public class Person
    {
        public Person(Department department)
        {
            Department = department;
        }

        public Department Department { get; }

        public string Describe() => "Managed by " + Department.Manager.Name;
    }
}
