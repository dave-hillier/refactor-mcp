namespace Drawing
{
    public abstract class Shape
    {
        protected string _name = "shape";

        public abstract double Area();

        public virtual string Describe() => _name;

        public string Label() => Describe() + ": " + Area();
    }
}
