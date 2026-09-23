namespace Staff
{
    public class Employee : Person
    {
        public string LastName()
        {
            return base.LastName();
        }

        public string Badge() => Greeting("Employee") + " (" + base.LastName() + ")";
    }
}
