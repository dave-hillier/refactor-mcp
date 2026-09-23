using System.Collections.Generic;

namespace Staff
{
    public class Employee
    {
        private readonly Person _person = new Person();

        public string LastName() => _person.LastName();

        public void Register(List<Person> people) => people.Add(_person);
    }
}
