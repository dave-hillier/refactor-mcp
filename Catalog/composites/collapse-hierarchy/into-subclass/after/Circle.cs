using System;

namespace Drawing
{
    public class Circle
    {
        public double Radius;
        protected string _name = "shape";

        public double Area() => Math.PI * Radius * Radius;

        public string Describe() => "circle";

        public string Label() => Describe() + ": " + Area();
    }
}
