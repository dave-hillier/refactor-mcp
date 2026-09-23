using System.Threading.Tasks;

namespace Shop;

public class Stock
{
    public async Task<int> LoadAsync()
    {
        await Task.Yield();
        return 12;
    }

    public async Task<int> TwiceAsync()
    {
        return await LoadAsync() * 2;
    }
}
