namespace Shop
{
    public class Customer
    {
        #region Names
        /// <summary>What friends call the customer.</summary>
        public string Nickname { get; set; } = "";

        // The name on invoices.
        public string Name { get; set; } = "";
        #endregion
    }
}
