public class Sample
{
    public string Show(int count, string name) => "#" + count + Format(name);

    private string Format(string value) => "'" + value + "'";
}
