public class Sample
{
    public decimal Discounted(decimal price, decimal rate)
    {
        return price * (/*[*/1 - rate/*]*/);
    }
}
