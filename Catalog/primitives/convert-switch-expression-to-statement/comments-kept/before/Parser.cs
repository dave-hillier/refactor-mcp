namespace Shop
{
    public class Parser
    {
        public int Digit(char c)
        {
            // Only binary digits are expected.
            return c /*^*/switch
            {
                // The usual case.
                '0' => 0,
                '1' => 1,
                _ => -1,
            };
        }
    }
}
