namespace Staff
{
    public class Person
    {
        public string Name { get; set; } = "";

        public string LastName() => Name.Split(" ")[^1];

        public string Greeting(string salutation) => salutation + " " + Name;
    }
}
