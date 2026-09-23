using System;

namespace Drawing
{
    public class Circle : Shape
    {
        public double Radius;

        public override double Area() => Math.PI * Radius * Radius;

        public override string Describe() => "circle";
    }
}
