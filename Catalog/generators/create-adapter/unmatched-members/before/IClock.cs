using System;

namespace Shop
{
    /// <summary>What the scheduler needs from a clock.</summary>
    public interface IClock
    {
        #region Time

        /// <summary>The current local time.</summary>
        DateTime Now { get; }

        // Tests freeze the clock with this.
        void Freeze(DateTime at);

        #endregion

        event EventHandler Ticked;
    }
}
