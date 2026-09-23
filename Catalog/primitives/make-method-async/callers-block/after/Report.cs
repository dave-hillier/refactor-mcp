namespace Stock
{
    public class Report
    {
        private readonly int _widgets;

        public Report(Inventory inventory)
        {
            _widgets = inventory.Available("widget").GetAwaiter().GetResult();
        }

        public string Summary(Inventory inventory)
        {
            return "Gadgets: " + inventory.Available("gadget").GetAwaiter().GetResult() + (inventory.InStock("widget") ? "" : " (no widgets)");
        }
    }
}
