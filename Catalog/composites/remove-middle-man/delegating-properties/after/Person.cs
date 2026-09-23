namespace Company
{
    public class Person
    {
        public Person(Department department)
        {
            Department = department;
        }

        public Department Department { get; }

        public string ManagerName() => Department.Manager.Name;

        public override string ToString() => Department.ToString();
    }
}
