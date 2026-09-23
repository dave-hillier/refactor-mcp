using Shop.Mail;

namespace Shop
{
    public class OrderService
    {
        private readonly string _prefix;
        private readonly Mailer _mailer;

        public OrderService(string prefix, Mailer mailer)
        {
            _prefix = prefix;
            _mailer = mailer;
        }

        public void Place(string order)
        {
            // Mail goes out as soon as the order is placed.
            _mailer.Send(_prefix + order);
        }
    }
}
