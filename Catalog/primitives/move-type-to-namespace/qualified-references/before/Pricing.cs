using static Shop.Billing.Tax;
using Levy = Shop.Billing.Tax;

namespace Shop
{
    public class Pricing
    {
        public decimal Gross(decimal net) => net + Shop.Billing.Tax.On(net);

        public decimal Rated(decimal net) => net * Rate;

        public decimal Levied(decimal net) => Levy.On(net);

        public decimal Global(decimal net) => global::Shop.Billing.Tax.On(net);
    }
}
