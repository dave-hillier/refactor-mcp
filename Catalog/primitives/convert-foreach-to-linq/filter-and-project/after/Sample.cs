using System.Collections.Generic;
using System.Linq;

public class Sample
{
    public List<string> ActiveNames(List<User> users)
    {
        var names = users.Where(user => user.IsActive).Select(user => user.Name).ToList();

        return names;
    }
}
