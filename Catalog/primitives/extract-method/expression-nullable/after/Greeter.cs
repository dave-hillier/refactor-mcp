namespace People
{
    public class Greeter
    {
        public string Greet(string? name)
        {
            var shown = NameOrGuest(name);
            return "Hello " + shown;
        }

        private string NameOrGuest(string? name)
        {
            return name ?? "guest";
        }
    }
}
