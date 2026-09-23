namespace Drawing
{
    public class Canvas
    {
        public string Caption(Circle shape) => shape.Label();

        public string Draw() => Caption(new Circle { Radius = 2 });
    }
}
