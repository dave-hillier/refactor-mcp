public class Sample
{
    public int CountDigits(string text)
    {
        var digits = 0;
        /*^*/foreach (char c in text)
        {
            if (char.IsDigit(c))
            {
                digits++;
            }
        }

        return digits;
    }
}
