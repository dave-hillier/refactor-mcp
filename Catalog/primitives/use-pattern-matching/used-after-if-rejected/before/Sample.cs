using System;

namespace Shop
{
    public class Sample
    {
        public void Print(object value)
        {
            var text = value as string;
            /*^*/if (text != null)
            {
                Console.WriteLine(text.Length);
            }

            Console.WriteLine(text);
        }
    }
}
