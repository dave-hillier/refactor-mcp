using System;

namespace Shop
{
    /// <summary>The items a customer is about to buy.</summary>
    [Serializable]
    // Kept small on purpose.
    partial class Basket
    {
        public int Count;
    }
}
