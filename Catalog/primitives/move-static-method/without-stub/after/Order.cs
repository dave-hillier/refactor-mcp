namespace Shop
{
    public class Order
    {
        public decimal Net { get; set; }

        public decimal Gross() => Net + TaxRules.Vat(Net);
    }
}
