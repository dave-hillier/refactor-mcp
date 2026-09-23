using Shop.Mail;

namespace Shop
{
    public class OrderService
    {
        private readonly string _prefix;
        private readonly Mailer mailer;

        public OrderService(string prefix, Mailer mailer)
        {
            _prefix = prefix;
            this.mailer = mailer;
        }

        public void Place(string order)
        {
            // Mail goes out as soon as the order is placed.
            var mailer = this.mailer;
            mailer.Send(_prefix + order);
        }
    }
}
