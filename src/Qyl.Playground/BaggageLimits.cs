using System.Diagnostics;

namespace Qyl.Playground;

// W3C baggage spec (https://www.w3.org/TR/baggage/) recommends caps to keep
// the header bounded as it traverses services. The .NET runtime does NOT
// enforce these — Activity.AddBaggage will happily grow forever. Enforce
// before injection or your downstream HTTP/gRPC clients will eventually
// reject the headers and break propagation entirely.
public static class BaggageLimits
{
    public const int MaxEntries = 64;
    public const int MaxKeyLength = 64;
    public const int MaxValueLength = 256;
    public const int MaxTotalBytes = 8 * 1024;

    public enum RejectReason
    {
        Accepted,
        NullActivity,
        EmptyKey,
        KeyTooLong,
        ValueTooLong,
        EntryCountExceeded,
        TotalSizeExceeded
    }

    public static RejectReason TryAddBaggage(Activity? activity, string key, string? value)
    {
        if (activity is null)
        {
            return RejectReason.NullActivity;
        }

        if (string.IsNullOrEmpty(key))
        {
            return RejectReason.EmptyKey;
        }

        if (key.Length > MaxKeyLength)
        {
            return RejectReason.KeyTooLong;
        }

        if (value is { Length: > MaxValueLength })
        {
            return RejectReason.ValueTooLong;
        }

        var count = 0;
        var approximateBytes = 0;
        foreach (var entry in activity.Baggage)
        {
            count++;
            approximateBytes += entry.Key.Length + (entry.Value?.Length ?? 0) + 2;
        }

        if (count >= MaxEntries)
        {
            return RejectReason.EntryCountExceeded;
        }

        var addedBytes = key.Length + (value?.Length ?? 0) + 2;
        if (approximateBytes + addedBytes > MaxTotalBytes)
        {
            return RejectReason.TotalSizeExceeded;
        }

        activity.AddBaggage(key, value);
        return RejectReason.Accepted;
    }
}
