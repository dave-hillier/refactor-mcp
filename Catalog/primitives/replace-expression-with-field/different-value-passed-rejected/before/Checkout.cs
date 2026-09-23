namespace Shop.Web
{
    public class Checkout
    {
        public void Complete(string order)
        {
            var service = new OrderService("web-", new Mail.Mailer("smtp.other.com"));
            service.Place(order);
        }
    }
}
