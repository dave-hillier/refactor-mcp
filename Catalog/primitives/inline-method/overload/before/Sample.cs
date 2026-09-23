public class Sample
{
    public string Show(int count, string name) => Format(count) + Format(name);

    private string Format(int value) => "#" + value;

    private string Format(string value) => "'" + value + "'";
}
