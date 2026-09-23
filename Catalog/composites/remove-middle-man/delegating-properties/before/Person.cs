namespace Company
{
    public class Person
    {
        public Person(Department department)
        {
            Department = department;
        }

        public Department Department { get; }

        public Employee Manager => Department.Manager;

        public string Code
        {
            get { return Department.Code; }
        }

        public string ManagerName() => Manager.Name;

        public override string ToString() => Department.ToString();
    }
}
