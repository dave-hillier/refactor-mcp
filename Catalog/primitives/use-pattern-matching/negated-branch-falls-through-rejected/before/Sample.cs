using System;

namespace Shop
{
    public class Sample
    {
        public void Print(object value)
        {
            /*^*/if (!(value is string))
            {
                Console.WriteLine("not text");
            }

            Console.WriteLine(((string)value).Length);
        }
    }
}
