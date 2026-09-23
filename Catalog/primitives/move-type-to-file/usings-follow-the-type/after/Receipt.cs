using System.Text;

namespace Shop
{
    public class Receipt
    {
        public string Print(Order order)
        {
            var text = new StringBuilder();
            text.Append("Total: ").Append(order.Total());
            return text.ToString();
        }
    }
}
