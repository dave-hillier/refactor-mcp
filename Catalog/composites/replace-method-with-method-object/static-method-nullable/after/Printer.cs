using System.Collections.Generic;

namespace Reports
{
    public static class Printer
    {
        public static void Print(IList<string> lines, string title)
        {
            new PrintJob(lines, title).Run();
        }
    }
}
