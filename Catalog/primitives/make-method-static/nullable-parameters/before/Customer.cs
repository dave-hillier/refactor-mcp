namespace Shop
{
    public class Customer
    {
        public string Name { get; set; } = "";

        public string? Title { get; set; }

        public string Greeting() => Title is null ? Name : Title + " " + Name;

        public string Card() => "Dear " + Greeting();
    }
}
