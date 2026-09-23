namespace Shop
{
    public class Order
    {
        private readonly Client _customer = new Client("Ada");

        public Client Buyer => _customer;
    }
}
