using System;
using System.Collections.Generic;

namespace Shop;

public class Report
{
    public string Summary(string customer, int quantity)
    {
        var line = new Line(customer, quantity);
        return line.Customer + ": " + line.Quantity;
    }
}

internal sealed class Line
{
    public Line(string customer, int quantity)
    {
        Customer = customer;
        Quantity = quantity;
    }

    public string Customer { get; }

    public int Quantity { get; }

    public override bool Equals(object obj)
    {
        return obj is Line other
            && EqualityComparer<string>.Default.Equals(Customer, other.Customer)
            && EqualityComparer<int>.Default.Equals(Quantity, other.Quantity);
    }

    public override int GetHashCode() => HashCode.Combine(Customer, Quantity);

    public override string ToString() => $"{{ Customer = {Customer}, Quantity = {Quantity} }}";
}
