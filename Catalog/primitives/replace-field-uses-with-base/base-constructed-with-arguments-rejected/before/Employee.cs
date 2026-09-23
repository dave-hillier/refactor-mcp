namespace Staff
{
    public class Employee : Person
    {
        private readonly Person _person = new Person();

        public Employee() : base("New starter")
        {
        }

        public string Badge() => "Employee " + _person.Name;
    }
}
