using System;

namespace Shop;

public class Item : IComparable<Item>
{
    public int Rank { get; set; }

    public int CompareTo(Item other)
    {
        return Rank.CompareTo(other.Rank);
    }
}
