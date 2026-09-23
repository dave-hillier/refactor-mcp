using System;

namespace Shop
{
    public interface IPrinter
    {
        void Print(string text);

        void Print(string text, int copies);
    }

    public class OldPrinter
    {
        public void Output(int code) => Console.WriteLine(code);

        public void Output(string text) => Console.WriteLine(text);

        public void Output(string text, int copies)
        {
            for (var i = 0; i < copies; i++)
                Console.WriteLine(text);
        }
    }
}
