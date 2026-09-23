using Shop.Mail;

namespace Shop
{
    public class PriorityService : OrderService
    {
        public PriorityService()
            : base("vip-", new Mailer("smtp.example.com"))
        {
        }
    }
}
