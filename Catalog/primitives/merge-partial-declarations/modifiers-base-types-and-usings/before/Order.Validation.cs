using System.Collections.Generic;
using System.Text;
using Shop.Rules;

namespace Shop;

sealed partial class Order : IValidatable
{
    public IEnumerable<string> Validate()
    {
        yield return "ok";
    }
}
