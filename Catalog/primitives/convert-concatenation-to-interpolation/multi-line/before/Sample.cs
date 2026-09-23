public class Sample
{
    public string Describe(string name, int count)
    {
        // The summary line.
        var text = /*^*/"Name: " +
                   name +
                   ", count: " +
                   count; // for the log
        return text;
    }
}
