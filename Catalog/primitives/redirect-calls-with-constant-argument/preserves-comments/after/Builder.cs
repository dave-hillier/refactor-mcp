namespace Shapes
{
    public class Builder
    {
        public Box Build()
        {
            var box = new Box();
            // The height comes first.
            box.SetHeight(10 /* fixed */); // tall
            return box;
        }
    }
}
