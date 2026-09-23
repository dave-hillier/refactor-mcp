public class Sample
{
    public decimal Discounted(decimal price, decimal rate)
    {
        decimal factor = 1 - rate;
        return price * factor;
    }
}
