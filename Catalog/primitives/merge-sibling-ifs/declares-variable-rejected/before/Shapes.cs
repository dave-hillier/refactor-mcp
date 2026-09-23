namespace Geometry
{
    public class Shapes
    {
        public int Sides(object shape)
        {
            /*^*/if (shape is string name && name.Length == 0)
            {
                return 0;
            }

            if (shape is null)
            {
                return 0;
            }

            return 1;
        }
    }
}
