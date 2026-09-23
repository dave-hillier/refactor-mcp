public class Sample
{
    public decimal Tax(bool taxable, decimal total, decimal rate)
    {
        if (taxable)
        {
            decimal tax = total * rate;
            return tax;
        }
        return 0;
    }
}
