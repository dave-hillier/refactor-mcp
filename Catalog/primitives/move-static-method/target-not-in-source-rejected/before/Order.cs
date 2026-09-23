namespace Shop
{
    public class Order
    {
        public static decimal Vat(decimal net) => net * 0.2m;
    }
}
