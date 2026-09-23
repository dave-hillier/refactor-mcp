using System;

namespace Shapes
{
    public class Box
    {
        private int _height;

        public void SetValue(string name, int value)
        {
            Console.WriteLine(name);
            if (name == "height")
                _height = value;
        }

        public int Height()
        {
            return _height;
        }
    }
}
