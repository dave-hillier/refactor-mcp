namespace Staff
{
    public class Employee
    {
        public readonly Person Person = new Person();

        public string LastName() => Person.LastName();
    }
}
