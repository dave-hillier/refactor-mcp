using System.Collections.Generic;

namespace Shop.Rules;

public interface IValidatable
{
    IEnumerable<string> Validate();
}
