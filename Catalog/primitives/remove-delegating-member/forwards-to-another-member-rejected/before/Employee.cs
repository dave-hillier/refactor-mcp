namespace Staff
{
    public class Employee : Person
    {
        public string Surname() => base.LastName();
    }
}
