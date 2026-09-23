using System;
using System.Collections.Generic;

namespace Shop
{
    public class Mailer
    {
        private readonly List<string> _sent = new List<string>();

        /// <summary>Queues a message.</summary>
        public void Send(string to, string subject, string body)
        {
            // Normalise the address first
            if (to == null)
                throw new ArgumentNullException(nameof(to));

            _sent.Add(to.Trim() + ": " + subject + body); // record it
        }
    }
}
