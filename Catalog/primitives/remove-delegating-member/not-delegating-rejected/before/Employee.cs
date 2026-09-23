namespace Staff
{
    public class Employee : Person
    {
        public new string LastName() => base.LastName().ToUpperInvariant();
    }
}
