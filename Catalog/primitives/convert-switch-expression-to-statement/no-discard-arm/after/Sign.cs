using System.Runtime.CompilerServices;

namespace Shop
{
    public class Sign
    {
        public string Describe(bool positive)
        {
            switch (positive)
            {
                case true:
                    return "positive";
                case false:
                    return "not positive";
                default:
                    throw new SwitchExpressionException(positive);
            }
        }
    }
}
