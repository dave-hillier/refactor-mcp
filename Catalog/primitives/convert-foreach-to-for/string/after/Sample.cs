public class Sample
{
    public int CountDigits(string text)
    {
        var digits = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsDigit(c))
            {
                digits++;
            }
        }

        return digits;
    }
}
