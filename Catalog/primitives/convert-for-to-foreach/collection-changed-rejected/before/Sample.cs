using System.Collections.Generic;

public class Sample
{
    public void Repeat(List<int> values)
    {
        /*^*/for (int i = 0; i < values.Count; i++)
        {
            if (values[i] > 10)
            {
                values.Add(values[i] - 10);
            }
        }
    }
}
