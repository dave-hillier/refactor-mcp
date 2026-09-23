public class Sample
{
    public decimal Net(decimal price, decimal rate)
    {
        var total = price;

        // Apply the discount.
        decimal discount = price * rate;
        return total - discount; // before tax
    }
}
