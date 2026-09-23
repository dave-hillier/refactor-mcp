namespace Shop;

public class Greeting
{
    public string Greet(string name)
    {
        return "Hello " + name + "!";
    }

    public string Morning()
    {
        return Greet("Ann") + Greet("Bob");
    }
}
