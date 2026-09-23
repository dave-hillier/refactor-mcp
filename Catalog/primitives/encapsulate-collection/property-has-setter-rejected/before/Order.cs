using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        private List<string> _tags = new List<string>();

        public List<string> Tags
        {
            get => _tags;
            set => _tags = value;
        }
    }
}
