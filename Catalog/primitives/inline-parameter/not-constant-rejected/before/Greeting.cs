namespace Shop;

public class Greeting
{
    public static readonly string Mark = "!";

    public string Greet(string name, string punctuation)
    {
        return "Hello " + name + punctuation;
    }

    public string Morning()
    {
        return Greet("Ann", Mark) + Greet("Bob", Mark);
    }
}
