public class Sample
{
    public string Describe<T>()
    {
        return Name() + "!";

        string /*^*/Name() => typeof(T).Name;
    }
}
