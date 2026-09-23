public class Sample
{
    public string Describe()
    {
        var /*^*/point = new { X = 1, Y = 2 };
        return point.X + "," + point.Y;
    }
}
