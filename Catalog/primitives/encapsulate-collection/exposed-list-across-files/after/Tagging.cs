namespace Shop
{
    public class Tagging
    {
        public int Retag(Order order)
        {
            order.AddTag("gift");
            order.RemoveTag("sale");
            var count = 0;
            foreach (var tag in order.Tags)
                count += tag.Length;
            return count + order.Tags.Count;
        }
    }
}
