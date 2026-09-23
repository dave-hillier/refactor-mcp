using Shop.Mail;

namespace Shop
{
    public class OrderService
    {
        private readonly string _prefix;

        public OrderService(string prefix)
        {
            _prefix = prefix;
        }

        public void Place(string order)
        {
            // Mail goes out as soon as the order is placed.
            var /*^*/mailer = new Mailer("smtp.example.com");
            mailer.Send(_prefix + order);
        }
    }
}
