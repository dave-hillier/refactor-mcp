namespace Staff
{
    public class Employee : Person
    {
        private readonly Person _person = new Person();

        public string Badge() => _person.Greeting("Employee");

        public Person AsPerson() => _person;
    }
}
