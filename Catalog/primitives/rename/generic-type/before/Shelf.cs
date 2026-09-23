using System.Collections.Generic;

namespace Shop
{
    public class Shelf
    {
        private readonly List<Box<int>> _boxes = new List<Box<int>>();

        public void Add(int item) => _boxes.Add(new Box<int>(item));
    }
}
