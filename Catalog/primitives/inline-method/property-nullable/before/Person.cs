namespace People
{
    public class Person
    {
        public string Name { get; set; } = "";

        public string? Nickname { get; set; }

        public string Display => Nickname ?? Name;
    }

    public class Badge
    {
        public int Width(Person person) => person.Display.Length * 8;
    }
}
