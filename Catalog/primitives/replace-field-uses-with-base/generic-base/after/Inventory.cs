using System.Collections.Generic;

namespace Shop
{
    public class Inventory : List<string>
    {
        public string Receive(string item)
        {
            Add(item);
            return this[0] + " of " + Count;
        }
    }
}
