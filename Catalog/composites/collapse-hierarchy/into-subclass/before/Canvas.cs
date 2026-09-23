namespace Drawing
{
    public class Canvas
    {
        public string Caption(Shape shape) => shape.Label();

        public string Draw() => Caption(new Circle { Radius = 2 });
    }
}
