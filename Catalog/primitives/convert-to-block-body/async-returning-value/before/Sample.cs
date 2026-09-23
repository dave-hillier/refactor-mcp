using System.Threading.Tasks;

public class Sample
{
    public async Task<int> CountAsync() => await Task.FromResult(3);
}
