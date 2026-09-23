namespace Shop;

public class Greeter(string greeting)
{
    private readonly string _greeting = "Hello";

    public string Greet(string name) => greeting + ", " + name + _greeting;
}
