using Shop.Mail;

namespace Shop
{
    public class OrderService
    {
        private readonly string _prefix;
        private Mailer _mailer;

        public OrderService(string prefix, Mailer mailer)
        {
            // Orders are labelled with the prefix.
            _prefix = prefix; // kept for Place

            // Nothing else to set up.
        }

        public void Place(string order)
        {
            var mailer = new Mailer("smtp.example.com");
            mailer.Send(_prefix + order);
        }
    }
}
