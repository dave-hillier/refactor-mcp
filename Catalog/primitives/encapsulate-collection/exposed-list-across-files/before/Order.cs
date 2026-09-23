using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        private readonly List<string> _tags = new List<string>();

        public List<string> Tags => _tags;

        public bool IsGift() => _tags.Contains("gift");
    }
}
