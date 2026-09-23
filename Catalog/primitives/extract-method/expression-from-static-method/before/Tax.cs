namespace Billing
{
    public static class Tax
    {
        private const decimal Rate = 0.2m;

        public static decimal Total(decimal amount)
        {
            return amount + /*[*/amount * Rate/*]*/;
        }
    }
}
