using System;
using System.Collections.Generic;

namespace GatherBuddy.Enums;

// Ordered by the progression of time throughout a fishing route.
[Flags]
public enum OceanTime : byte
{
    ´Ó²»  = 0,
    ÈÕÂä = 0x01,
    Ò¹Íí  = 0x02,
    °×Öç    = 0x04,

    ×ÜÊÇ = ÈÕÂä | Ò¹Íí | °×Öç,
}

public static class OceanTimeExtensions
{
    public static OceanTime Next(this OceanTime time)
        => time switch
        {
            OceanTime.ÈÕÂä => OceanTime.Ò¹Íí,
            OceanTime.Ò¹Íí  => OceanTime.°×Öç,
            OceanTime.°×Öç    => OceanTime.ÈÕÂä,
            _                => OceanTime.ÈÕÂä,
        };

    public static OceanTime Previous(this OceanTime time)
        => time switch
        {
            OceanTime.ÈÕÂä => OceanTime.°×Öç,
            OceanTime.Ò¹Íí  => OceanTime.ÈÕÂä,
            OceanTime.°×Öç    => OceanTime.Ò¹Íí,
            _                => OceanTime.ÈÕÂä,
        };

    public static IEnumerable<OceanTime> Enumerate(this OceanTime time)
    {
        if (time.HasFlag(OceanTime.ÈÕÂä))
            yield return OceanTime.ÈÕÂä;
        if (time.HasFlag(OceanTime.Ò¹Íí))
            yield return OceanTime.Ò¹Íí;
        if (time.HasFlag(OceanTime.°×Öç))
            yield return OceanTime.°×Öç;
    }
}
