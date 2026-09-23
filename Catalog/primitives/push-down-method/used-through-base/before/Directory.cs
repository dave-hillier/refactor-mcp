using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public static class Directory
    {
        public static List<string> Describe(List<Employee> staff) => staff.Select(e => e.Describe()).ToList();
    }
}
