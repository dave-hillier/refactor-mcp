public class Sample
{
    private readonly decimal[] _data = new decimal[3];

    public decimal Total()
    {
        decimal total = 0;
        /*^*/for (int i = 0; i < _data.Length; i++)
            total += _data[i];
        return total;
    }
}
