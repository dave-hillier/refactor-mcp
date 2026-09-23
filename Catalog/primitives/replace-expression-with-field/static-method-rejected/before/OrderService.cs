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
            DefaultMailer().Send(_prefix + order);
        }

        public static Mailer DefaultMailer()
        {
            return /*[*/new Mailer("smtp.example.com")/*]*/;
        }
    }
}
