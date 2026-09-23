namespace Shapes
{
    public class Builder
    {
        public Box Build()
        {
            var box = new Box();
            box.SetValue("height", 10);
            box.SetValue(null, 2);
            return box;
        }
    }
}
