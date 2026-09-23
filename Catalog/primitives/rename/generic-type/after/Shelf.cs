using System.Collections.Generic;

namespace Shop
{
    public class Shelf
    {
        private readonly List<Container<int>> _boxes = new List<Container<int>>();

        public void Add(int item) => _boxes.Add(new Container<int>(item));
    }
}
