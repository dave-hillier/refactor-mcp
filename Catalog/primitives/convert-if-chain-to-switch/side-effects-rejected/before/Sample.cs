using System;

namespace Shop
{
    public class Sample
    {
        private int _next;

        public string Take()
        {
            /*^*/if (Next() == 1)
            {
                return "one";
            }
            else if (Next() == 2)
            {
                return "two";
            }

            return "other";
        }

        private int Next() => _next++;
    }
}
