using System;
using Dalamud.Game;
using Dalamud.Plugin.Services;

namespace GatherBuddy.SeFunctions;

public interface ISigScannerWrapper
{
    IntPtr GetStaticAddressFromSig(string signature, int offset = 0);
    IntPtr ScanText(string signature);
    IntPtr ModuleBaseAddress { get; }
}

public class SigScannerWrapper : ISigScannerWrapper
{
    private readonly SigScanner _sigScanner;

    public SigScannerWrapper(IGameInteropProvider interop)
    {
        _sigScanner = new SigScanner();
    }

    public IntPtr GetStaticAddressFromSig(string signature, int offset = 0)
    {
        return _sigScanner.GetStaticAddressFromSig(signature, offset);
    }

    public IntPtr ScanText(string signature)
    {
        if (_sigScanner.TryScanText(signature, out var ptr))
            return ptr;
        
        GatherBuddy.Log.Warning($"Failed to find signature: {signature}");
        return IntPtr.Zero;
    }

    public IntPtr ModuleBaseAddress 
        => _sigScanner.Module.BaseAddress;
}
