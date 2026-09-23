namespace People
{
    public class Person
    {
        public string Name { get; set; } = "";

        public string? Nickname { get; set; }
    }

    public class Badge
    {
        public int Width(Person person) => (person.Nickname ?? person.Name).Length * 8;
    }
}
