namespace Shapes
{
    public class Box
    {
        private int _height;
        private int _width;
        private int _changes;

        public void SetValue(string name, int value)
        {
            if (name == "height")
                _height = value;
            else if (name == "width")
                _width = value;
            _changes++;
        }

        public int Changes()
        {
            return _changes + _height + _width;
        }
    }
}
