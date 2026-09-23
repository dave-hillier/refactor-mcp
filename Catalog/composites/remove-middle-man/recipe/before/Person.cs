namespace Company
{
    public class Person
    {
        public Person(Department department)
        {
            Department = department;
        }

        public Department Department { get; }

        public Employee GetManager()
        {
            return Department.Manager;
        }

        public decimal Budget(int year) => Department.Budget(year);
    }
}
