using System;
using Shop.Logging;

namespace Shop.Reports
{
    public class Report
    {
        private readonly ILog _log = new Logging.ConsoleLog();

        public void Print() => _log.Info(DateTime.Now.ToString());
    }
}
