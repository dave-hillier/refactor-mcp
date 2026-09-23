namespace Shop.Web
{
    public class Checkout
    {
        public void Complete(string order)
        {
            var service = new OrderService("web-");
            service.Place(order);
        }
    }
}
