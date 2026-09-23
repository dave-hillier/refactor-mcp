namespace Shop;

public class Greeting
{
    public string Greet(string name, string punctuation)
    {
        return "Hello " + name + punctuation;
    }

    public string Morning()
    {
        return Greet("Ann", "!") + Greet("Bob", "?");
    }
}
