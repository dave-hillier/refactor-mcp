namespace Staff
{
    public class Employee
    {
        private Person _person = new Person();

        public string LastName() => _person.LastName();

        public void Become(Person person) => _person = person;
    }
}
