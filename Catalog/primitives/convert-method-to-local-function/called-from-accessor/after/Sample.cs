public class Sample
{
    private int[] _values = { 1, 2 };

    public int Total
    {
        get
        {
            return Sum();

            int Sum()
            {
                var sum = 0;
                foreach (var value in _values)
                    sum += value;
                return sum;
            }
        }
    }
}
