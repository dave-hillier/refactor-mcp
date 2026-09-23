public class Sample
{
    public string Describe(int number)
    {
        return /*^*/Format(number);
    }

    private static string Format(int number) => "#" + number;
}
