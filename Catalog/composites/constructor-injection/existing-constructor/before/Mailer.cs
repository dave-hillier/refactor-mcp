using System.Collections.Generic;

namespace Shop.Mail
{
    public class Mailer
    {
        private readonly string _host;
        private readonly List<string> _sent = new List<string>();

        public Mailer(string host)
        {
            _host = host;
        }

        public void Send(string message)
        {
            _sent.Add(_host + ": " + message);
        }
    }
}
