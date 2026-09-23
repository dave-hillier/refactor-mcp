namespace Staff
{
    public class Employee : Person
    {
        public new string Greeting(string salutation = "Hello") => base.Greeting(salutation);

        public string Badge() => Greeting();
    }
}
