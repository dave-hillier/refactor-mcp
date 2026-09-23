namespace Shop;

public class Item
{
    public int Rank { get; set; }

    public override int GetHashCode()
    {
        return Rank;
    }
}
