using System;

namespace Shapes
{
    public class Box
    {
        private int _height;
        private int _width;

        public void SetValue(string name, int value)
        {
            if (name == "height")
            {
                _height = value;
                return;
            }
            if (name == "width")
            {
                /*[*/_width = value;/*]*/
                return;
            }
            throw new ArgumentException("Unknown dimension " + name);
        }

        public int Area()
        {
            return _height * _width;
        }
    }
}
