public class Row
{
    private readonly int[] _cells = new int[4];

    public int Count => _cells.Length;

    public int this[int index] => _cells[index];
}

public class Sample
{
    public int Sum(Row row)
    {
        var total = 0;
        /*^*/for (int i = 0; i < row.Count; i++)
        {
            total += row[i];
        }

        return total;
    }
}
