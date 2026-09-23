using System;

namespace Diagnostics
{
    public class Logger
    {
        public Action For(string message)
        {
            return () => /*[*/Console.WriteLine(message)/*]*/;
        }
    }
}
