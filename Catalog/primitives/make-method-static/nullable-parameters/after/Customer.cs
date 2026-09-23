namespace Shop
{
    public class Customer
    {
        public string Name { get; set; } = "";

        public string? Title { get; set; }

        public static string Greeting(string? title, string name) => title is null ? name : title + " " + name;

        public string Card() => "Dear " + Greeting(Title, Name);
    }
}
