using Shop.Mail;

namespace Shop
{
    public class OrderService
    {
        private readonly string _prefix;
        private Mailer mailer;

        public OrderService(string prefix, Mailer mailer)
        {
            _prefix = prefix;
            this.mailer = mailer;
        }

        public void Place(string order)
        {
            var mailer = new Mailer("smtp.example.com");
            mailer.Send(_prefix + order);
        }
    }
}
