namespace Shop
{
    public class Order
    {
        public decimal Net { get; set; }

        public static decimal Vat(decimal net) => net * 0.2m;

        public decimal Gross() => Net + Vat(Net);
    }
}
