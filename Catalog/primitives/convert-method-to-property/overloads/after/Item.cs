namespace Shop
{
    public class Item
    {
        public decimal Price => 10m;

        public decimal GetPrice(int quantity) => Price * quantity;
    }

    public class Till
    {
        public decimal Charge(Item item) => item.Price + item.GetPrice(2);
    }
}
