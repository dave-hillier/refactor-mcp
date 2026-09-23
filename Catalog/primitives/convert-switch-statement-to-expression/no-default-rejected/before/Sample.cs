using System;

namespace Shop
{
    public class Sample
    {
        public void Print(int code)
        {
            string name = "";
            /*^*/switch (code)
            {
                case 1:
                    name = "one";
                    break;
                case 2:
                    name = "two";
                    break;
            }

            Console.WriteLine(name);
        }
    }
}
