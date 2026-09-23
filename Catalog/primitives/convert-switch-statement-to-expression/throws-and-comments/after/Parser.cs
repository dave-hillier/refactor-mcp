using System;

namespace Shop
{
    public class Parser
    {
        public int Digit(char c)
        {
            return c switch
            {
                // The usual case.
                '0' => 0,
                '1' => 1, // binary is enough
                _ => throw new ArgumentException("not a digit"),
            };
        }
    }
}
