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
            Place("opening");
            _mailer = mailer;
        }

        public void Place(string order)
        {
            // Mail goes out as soon as the order is placed.
            var mailer = /*[*/new Mailer("smtp.example.com")/*]*/;
            mailer.Send(_prefix + order);
        }
    }
}
