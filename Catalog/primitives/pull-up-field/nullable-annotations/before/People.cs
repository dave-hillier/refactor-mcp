namespace Shop
{
    public class Employee
    {
        public string Name = "";
    }

    public class Manager : Employee
    {
        public string? Nickname;

        public string Greeting() => Nickname ?? Name;
    }
}
