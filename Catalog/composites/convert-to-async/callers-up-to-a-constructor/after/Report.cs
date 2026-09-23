using System.Threading.Tasks;

namespace Stock
{
    public class Report
    {
        private readonly int _widgets;

        public Report(Inventory inventory)
        {
            _widgets = inventory.Available("widget").GetAwaiter().GetResult();
        }

        public async Task<string> Summary(Inventory inventory)
        {
            return "Gadgets: " + await inventory.Available("gadget") + (await inventory.InStock("widget") ? "" : " (no widgets)");
        }
    }
}
