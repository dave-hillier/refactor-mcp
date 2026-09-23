namespace Shop
{
    public class Sample
    {
        private object _value = "";

        public int Length()
        {
            /*^*/if (_value is string)
            {
                return ((string)_value).Length;
            }

            return 0;
        }
    }
}
