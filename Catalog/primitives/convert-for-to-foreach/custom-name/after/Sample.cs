public class Sample
{
    private readonly decimal[] _data = new decimal[3];

    public decimal Total()
    {
        decimal total = 0;
        foreach (decimal price in _data)
            total += price;
        return total;
    }
}
