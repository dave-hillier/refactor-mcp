using System;

namespace Shop
{
    public interface ILogger
    {
        void Log(string message);
    }

    public class LegacyLogger
    {
        public void Write(string message) => Console.WriteLine(message);
    }
}
