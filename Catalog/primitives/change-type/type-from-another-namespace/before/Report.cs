using System;

namespace Shop.Reports
{
    public class Report
    {
        private readonly Logging.ConsoleLog _log = new Logging.ConsoleLog();

        public void Print() => _log.Info(DateTime.Now.ToString());
    }
}
