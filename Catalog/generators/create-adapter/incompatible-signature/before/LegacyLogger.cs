using System;

namespace Shop
{
    public interface ILogger
    {
        void Log(string message);
    }

    public class LegacyLogger
    {
        public void Write(int code) => Console.WriteLine(code);
    }
}
