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
                SetHeight(value);
                return;
            }
            if (name == "width")
            {
                SetWidth(value);
                return;
            }
            throw new ArgumentException("Unknown dimension " + name);
        }

        public void SetHeight(int value)
        {
            _height = value;
        }

        public void SetWidth(int value)
        {
            _width = value;
        }

        public void Flatten()
        {
            SetValue("height", 0);
        }

        public int Area()
        {
            return _height * _width;
        }
    }
}
