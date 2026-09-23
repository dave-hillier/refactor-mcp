namespace People
{
    public class Greeter
    {
        public string Greet(string? name)
        {
            var shown = /*[*/name ?? "guest"/*]*/;
            return "Hello " + shown;
        }
    }
}
