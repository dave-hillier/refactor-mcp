namespace Shop;

public class Greeting
{
    public string Greet(string name, string punctuation)
    {
        punctuation = punctuation.Trim();
        return "Hello " + name + punctuation;
    }

    public string Morning()
    {
        return Greet("Ann", "!") + Greet("Bob", "!");
    }
}
