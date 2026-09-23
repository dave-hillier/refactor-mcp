public class Sample
{
    public decimal Net(decimal price, decimal rate)
    {
        var total = price;

        // Apply the discount.
        return total - /*[*/price * rate/*]*/; // before tax
    }
}
