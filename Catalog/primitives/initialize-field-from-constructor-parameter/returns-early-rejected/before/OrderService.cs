using Shop.Mail;

namespace Shop
{
    public class OrderService
    {
        private readonly string _prefix;
        private Mailer _mailer;

        public OrderService(string prefix, Mailer mailer)
        {
            _prefix = prefix;
            if (prefix.Length == 0)
                return;
            _prefix = prefix + "-";
        }

        public void Place(string order)
        {
            var mailer = new Mailer("smtp.example.com");
            mailer.Send(_prefix + order);
        }
    }
}
