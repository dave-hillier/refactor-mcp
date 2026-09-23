namespace Shapes
{
    public class Builder
    {
        public Box Build(string dimension, int size)
        {
            var box = new Box();
            box.SetValue("height", 10);
            box.SetValue("width", size * 2);
            box.SetValue(dimension, size);
            return box;
        }
    }
}
