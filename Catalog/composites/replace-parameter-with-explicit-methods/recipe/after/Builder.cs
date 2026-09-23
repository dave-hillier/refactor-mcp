namespace Shapes
{
    public class Builder
    {
        public Box Build(string dimension, int size)
        {
            var box = new Box();
            box.SetHeight(10);
            box.SetWidth(size * 2);
            box.SetValue(dimension, size);
            return box;
        }
    }
}
