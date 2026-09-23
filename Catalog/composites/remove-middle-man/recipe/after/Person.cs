namespace Company
{
    public class Person
    {
        public Person(Department department)
        {
            Department = department;
        }

        public Department Department { get; }
    }
}
