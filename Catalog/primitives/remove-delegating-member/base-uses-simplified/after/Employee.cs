namespace Staff
{
    public class Employee : Person
    {
        public string Badge() => Greeting("Employee") + " (" + LastName() + ")";
    }
}
