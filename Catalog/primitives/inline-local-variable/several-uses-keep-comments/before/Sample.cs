using System;

public class Sample
{
    public bool IsLarge(int price, int quantity, int limit)
    {
        // Price before tax.
        var /*^*/net = price * quantity;
        Console.WriteLine(net);
        return net > limit;
    }
}
