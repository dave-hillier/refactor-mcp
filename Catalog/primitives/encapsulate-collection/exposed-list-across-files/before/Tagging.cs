namespace Shop
{
    public class Tagging
    {
        public int Retag(Order order)
        {
            order.Tags.Add("gift");
            order.Tags.Remove("sale");
            var count = 0;
            foreach (var tag in order.Tags)
                count += tag.Length;
            return count + order.Tags.Count;
        }
    }
}
