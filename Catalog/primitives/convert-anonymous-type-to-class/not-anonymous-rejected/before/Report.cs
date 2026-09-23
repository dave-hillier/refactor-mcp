using System.Text;

namespace Shop;

public class Report
{
    public string Summary(string customer) => /*^*/new StringBuilder(customer).ToString();
}
