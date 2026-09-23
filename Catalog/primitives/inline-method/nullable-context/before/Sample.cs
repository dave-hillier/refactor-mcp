public class Sample
{
    public string Describe(string? name) => OrDefault(name);

    private static string OrDefault(string? value) => value ?? "anonymous";
}
