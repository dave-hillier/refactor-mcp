namespace Shapes
{
    public class Builder
    {
        public Box Build()
        {
            var box = new Box();
            box.SetHeight(10);
            box.SetValue(null, 2);
            return box;
        }
    }
}
