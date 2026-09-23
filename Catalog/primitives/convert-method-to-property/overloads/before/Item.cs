namespace Shop
{
    public class Item
    {
        public decimal GetPrice() => 10m;

        public decimal GetPrice(int quantity) => GetPrice() * quantity;
    }

    public class Till
    {
        public decimal Charge(Item item) => item.GetPrice() + item.GetPrice(2);
    }
}
