using System;

namespace Shop
{
    public class Parser
    {
        public int Digit(char c)
        {
            /*^*/switch (c)
            {
                // The usual case.
                case '0':
                    return 0;
                case '1':
                    return 1; // binary is enough
                default:
                    throw new ArgumentException("not a digit");
            }
        }
    }
}
