public class Sample
{
    public string Describe<T>()
    {
        return Name<T>() + "!";
    }

    private static string Name<T>() => typeof(T).Name;
}
