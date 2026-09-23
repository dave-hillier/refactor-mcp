public class Sample
{
    public int Sum(int a, int b)
    {
        var total = a;
        var /*^*/x = b;
        return total + x;
    }
}
