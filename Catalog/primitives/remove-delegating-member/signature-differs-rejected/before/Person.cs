namespace Staff
{
    public class Person
    {
        public string Name { get; set; } = "";

        public string Greeting(string salutation = "Dear") => salutation + " " + Name;
    }
}
