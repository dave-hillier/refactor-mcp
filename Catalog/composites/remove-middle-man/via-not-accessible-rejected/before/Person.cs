namespace Company
{
    public class Person
    {
        private readonly Department _department;

        public Person(Department department)
        {
            _department = department;
        }

        public Employee GetManager() => _department.Manager;
    }
}
