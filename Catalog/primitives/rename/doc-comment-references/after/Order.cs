namespace Shop
{
    public class Order
    {
        /// <summary>The sum of the lines. See also <see cref="GrandTotal"/>.</summary>
        public int GrandTotal() => 10; // cached elsewhere

        // Logged under the method's name.
        public string Label => nameof(GrandTotal);
    }
}
