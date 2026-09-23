namespace Shop;

public class Greeting
{
    public virtual string Greet(string name, string punctuation)
    {
        return "Hello " + name + punctuation;
    }

    public string Morning()
    {
        return Greet("Ann", "!");
    }
}
