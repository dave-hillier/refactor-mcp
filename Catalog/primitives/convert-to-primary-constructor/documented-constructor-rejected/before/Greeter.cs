namespace Shop;

public class Greeter
{
    private readonly string _greeting;

    /// <summary>Creates a greeter.</summary>
    /// <param name="greeting">The word to greet with.</param>
    public Greeter(string greeting)
    {
        _greeting = greeting;
    }

    public string Greet(string name) => _greeting + ", " + name;
}
