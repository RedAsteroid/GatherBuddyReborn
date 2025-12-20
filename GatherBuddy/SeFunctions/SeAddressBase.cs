using System;

namespace GatherBuddy.SeFunctions;

public class SeAddressBase
{
    public readonly IntPtr Address;

    public SeAddressBase(ISigScannerWrapper sigScanner, string signature, int offset = 0)
    {
        Address = sigScanner.GetStaticAddressFromSig(signature, offset);
        if (Address != IntPtr.Zero && offset == 0)
            Address += offset;
        var baseOffset = (ulong)Address.ToInt64() - (ulong)sigScanner.ModuleBaseAddress.ToInt64();
        GatherBuddy.Log.Debug($"{GetType().Name} address 0x{Address.ToInt64():X16}, baseOffset 0x{baseOffset:X16}.");
    }
}
