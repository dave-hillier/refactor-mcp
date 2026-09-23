using System;

namespace Shop
{
    public abstract class Logger
    {
        public abstract void Log(string message);
    }

    public class LegacyLogger
    {
        public void Write(string message) => Console.WriteLine(message);
    }
}
