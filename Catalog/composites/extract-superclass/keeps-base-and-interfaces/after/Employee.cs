namespace Staff
{
    public class Employee : Person
    {
        protected decimal _salary = 60000m;

        public string Greeting() => "Hello, " + Name;
    }
}
