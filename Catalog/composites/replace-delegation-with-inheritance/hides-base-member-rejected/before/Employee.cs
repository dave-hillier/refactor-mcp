namespace Staff
{
    public class Employee
    {
        private readonly Person _person = new Person();

        public string LastName() => _person.LastName();

        public string Greeting(string salutation) => salutation + " " + _person.LastName();
    }
}
