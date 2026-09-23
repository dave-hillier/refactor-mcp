using System;
using System.Collections.Generic;
using System.Linq;

namespace Bank
{
    public class Entry
    {
        public Entry(DateTime date, decimal amount)
        {
            Date = date;
            Amount = amount;
        }

        public DateTime Date { get; }

        public decimal Amount { get; }
    }

    public class Account
    {
        private readonly List<Entry> _entries = new List<Entry>();

        public decimal TotalBetween(DateRange range)
        {
            return _entries.Where(e => e.Date >= range.Start && e.Date <= range.End).Sum(e => e.Amount);
        }
    }

    public readonly record struct DateRange(DateTime Start, DateTime End);
}
