namespace Shapes
{
    public class Builder
    {
        public Box Build(Box box)
        {
            box?.SetHeight(value: 3);
            new Box().SetHeight(4);
            return box;
        }
    }
}
