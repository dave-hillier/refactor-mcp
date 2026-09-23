namespace Shop
{
    public class Document
    {
        protected decimal Rate { get; set; } = 0.2m;
    }

    public class Order : Document
    {
        public decimal Tax(decimal amount)
        {
            decimal tax = amount * Rate;
            return tax;
        }
    }
}
