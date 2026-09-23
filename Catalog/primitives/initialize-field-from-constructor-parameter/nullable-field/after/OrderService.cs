namespace Shop
{
    public interface IMailer
    {
        void Send(string message);
    }

    public class Mailer : IMailer
    {
        public void Send(string message)
        {
        }
    }

    public class OrderService
    {
        private IMailer? _mailer;

        public OrderService(Mailer mailer)
        {
            _mailer = mailer;
        }
    }
}
