using System.Threading.Tasks;

public class Sample
{
    public async Task<int> CountAsync()
    {
        return await Task.FromResult(3);
    }
}
