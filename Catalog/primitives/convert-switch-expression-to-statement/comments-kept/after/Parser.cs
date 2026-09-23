namespace Shop
{
    public class Parser
    {
        public int Digit(char c)
        {
            // Only binary digits are expected.
            switch (c)
            {
                // The usual case.
                case '0':
                    return 0;
                case '1':
                    return 1;
                default:
                    return -1;
            }
        }
    }
}
