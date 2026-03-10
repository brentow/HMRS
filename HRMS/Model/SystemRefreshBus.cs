using System;

namespace HRMS.Model
{
    public sealed class SystemDataChangedEventArgs : EventArgs
    {
        public string Source { get; }

        public SystemDataChangedEventArgs(string source)
        {
            Source = string.IsNullOrWhiteSpace(source) ? "Unknown" : source.Trim();
        }
    }

    public static class SystemRefreshBus
    {
        public static event EventHandler<SystemDataChangedEventArgs>? DataChanged;

        public static void Raise(string source)
        {
            DataChanged?.Invoke(null, new SystemDataChangedEventArgs(source));
        }
    }
}
