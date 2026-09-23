namespace Staff
{
    public class Employee
    {
        private readonly Person _person;

        public Employee(Person person)
        {
            _person = person;
        }

        public string LastName() => _person.LastName();
    }
}
