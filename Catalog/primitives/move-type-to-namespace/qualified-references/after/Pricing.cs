using static Shop.Accounts.Tax;
using Levy = Shop.Accounts.Tax;

namespace Shop
{
    public class Pricing
    {
        public decimal Gross(decimal net) => net + Shop.Accounts.Tax.On(net);

        public decimal Rated(decimal net) => net * Rate;

        public decimal Levied(decimal net) => Levy.On(net);

        public decimal Global(decimal net) => global::Shop.Accounts.Tax.On(net);
    }
}
