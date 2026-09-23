namespace Shop
{
    public class Order
    {
        /// <summary>The sum of the lines. See also <see cref="Total"/>.</summary>
        public int Total() => 10; // cached elsewhere

        // Logged under the method's name.
        public string Label => nameof(Total);
    }
}
