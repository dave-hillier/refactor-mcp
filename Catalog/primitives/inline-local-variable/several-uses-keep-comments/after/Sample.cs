using System;

public class Sample
{
    public bool IsLarge(int price, int quantity, int limit)
    {
        // Price before tax.
        Console.WriteLine(price * quantity);
        return price * quantity > limit;
    }
}
