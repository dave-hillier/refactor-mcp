using System.Collections.Generic;
using System.Linq;

public class Sample
{
    private readonly List<string> _names = new List<string>();

    public void AddActive(List<User> users)
    {
        _names.AddRange(users.Where(user => user.IsActive).Select(user => user.Name));
    }
}
