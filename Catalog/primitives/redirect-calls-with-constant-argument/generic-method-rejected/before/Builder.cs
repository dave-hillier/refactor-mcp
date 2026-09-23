namespace Shapes
{
    public class Builder
    {
        public Box Build()
        {
            var box = new Box();
            box.SetValue<string>("height", 10);
            return box;
        }
    }
}
