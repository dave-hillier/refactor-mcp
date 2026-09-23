namespace Shop
{
    public class Employee
    {
        public string Name = "";
        public string? Nickname;
    }

    public class Manager : Employee
    {
        public string Greeting() => Nickname ?? Name;
    }
}
