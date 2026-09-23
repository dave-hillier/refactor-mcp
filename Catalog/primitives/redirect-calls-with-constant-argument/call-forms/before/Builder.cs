namespace Shapes
{
    public class Builder
    {
        public Box Build(Box box)
        {
            box?.SetValue(value: 3, name: "height");
            new Box().SetValue("height", 4);
            return box;
        }
    }
}
