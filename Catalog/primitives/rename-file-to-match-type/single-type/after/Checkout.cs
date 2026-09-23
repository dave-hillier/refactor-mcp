namespace Shop
{
    public class Checkout
    {
        public void Pay(Account account) => account.Post(10m);
    }
}
