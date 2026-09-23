namespace Shop;

public class Basket
{
    public Basket(string owner)
    {
        Owner = owner;
    }

    public string Owner { get; }

    public int Count { get; set; }
}
