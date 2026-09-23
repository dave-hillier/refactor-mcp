namespace Shop
{
    public class Mailer
    {
        public Mailer(string host)
        {
            Host = host;
        }

        public string Host { get; }
    }

    public class OrderService
    {
        public string Place(string order, string host)
        {
            var /*^*/mailer = new Mailer(host);
            return mailer.Host + order;
        }
    }
}
