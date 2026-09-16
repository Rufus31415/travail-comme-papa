using System.Runtime.InteropServices;

namespace TravailCommePapa;

/// <summary>
/// Volume général de Windows (périphérique de sortie par défaut), via l'API Core Audio.
/// Le hook clavier avalant les touches multimédia, c'est le seul moyen pour le parent
/// de couper le son ou de le baisser sans quitter l'application.
/// </summary>
internal static class SystemVolume
{
    private static readonly Guid DeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid AudioEndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private const int ERender = 0;              // flux de sortie
    private const int EMultimedia = 1;          // rôle « multimédia »
    private const int ClsCtxInprocServer = 1;

    private static Guid _noEvent = Guid.Empty;

    /// <summary>Coupe ou rétablit le son. Renvoie l'état obtenu, ou null si le volume est inaccessible.</summary>
    public static bool? ToggleMute()
    {
        var volume = Endpoint();
        if (volume == null) return null;
        try
        {
            if (volume.GetMute(out bool muted) != 0) return null;
            if (volume.SetMute(!muted, ref _noEvent) != 0) return null;
            return !muted;
        }
        catch { return null; }
        finally { Marshal.ReleaseComObject(volume); }
    }

    /// <summary>Règle le volume général (0 à 1) et rétablit le son s'il était coupé.</summary>
    public static bool SetLevel(float level)
    {
        var volume = Endpoint();
        if (volume == null) return false;
        try
        {
            if (volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref _noEvent) != 0) return false;
            volume.SetMute(false, ref _noEvent); // un volume réglé sans être entendu n'aiderait personne
            return true;
        }
        catch { return false; }
        finally { Marshal.ReleaseComObject(volume); }
    }

    private static IAudioEndpointVolume? Endpoint()
    {
        object? enumerator = null, device = null;
        try
        {
            var type = Type.GetTypeFromCLSID(DeviceEnumeratorClsid);
            if (type == null) return null;
            enumerator = Activator.CreateInstance(type);
            if (enumerator is not IMMDeviceEnumerator e) return null;
            if (e.GetDefaultAudioEndpoint(ERender, EMultimedia, out var dev) != 0 || dev == null) return null;
            device = dev;

            var iid = AudioEndpointVolumeIid;
            if (dev.Activate(ref iid, ClsCtxInprocServer, IntPtr.Zero, out object volume) != 0) return null;
            return volume as IAudioEndpointVolume;
        }
        catch { return null; }
        finally
        {
            if (device != null) Marshal.ReleaseComObject(device);
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
        }
    }
}

// Les méthodes doivent être déclarées dans l'ordre exact de la vtable COM, même celles qu'on n'utilise pas.

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                               [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
