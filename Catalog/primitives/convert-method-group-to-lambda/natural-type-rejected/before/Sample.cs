public class Sample
{
    public string Run()
    {
        var format = /*^*/Format;
        return format(3);
    }

    private static string Format(int number) => "#" + number;
}
