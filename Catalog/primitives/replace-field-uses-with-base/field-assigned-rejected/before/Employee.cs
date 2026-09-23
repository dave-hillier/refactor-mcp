namespace Staff
{
    public class Employee : Person
    {
        private Person _person = new Person();

        public string Badge() => _person.Greeting("Employee");

        public void Reset()
        {
            _person = new Person();
        }
    }
}
