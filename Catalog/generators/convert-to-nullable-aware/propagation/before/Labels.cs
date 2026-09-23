using System;

namespace Shop
{
    public class Labels
    {
        public void Print(int code)
        {
            string title = Lookup(code);
            Write(title);
        }

        private static string Lookup(int code)
        {
            return code == 0 ? null : "Code " + code;
        }

        private static void Write(string text)
        {
            Console.WriteLine(text ?? "(none)");
        }
    }
}
