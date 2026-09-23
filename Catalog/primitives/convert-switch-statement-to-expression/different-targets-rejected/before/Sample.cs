using System;

namespace Shop
{
    public class Sample
    {
        public void Print(int code)
        {
            string name = "";
            string other = "";
            /*^*/switch (code)
            {
                case 1:
                    name = "one";
                    break;
                default:
                    other = "other";
                    break;
            }

            Console.WriteLine(name + other);
        }
    }
}
