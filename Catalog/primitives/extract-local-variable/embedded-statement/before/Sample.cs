public class Sample
{
    public decimal Tax(bool taxable, decimal total, decimal rate)
    {
        if (taxable)
            return /*[*/total * rate/*]*/;
        return 0;
    }
}
