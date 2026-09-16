using System.Runtime.InteropServices;
using static TravailCommePapa.NativeMethods;

namespace TravailCommePapa;

/// <summary>
/// Désactive temporairement les raccourcis d'accessibilité qui ouvrent des popups
/// (Shift x5 = touches rémanentes, Maj droite 8 s = touches filtres, Verr.Num 5 s = touches bascules),
/// neutralise le bouton d'alimentation et empêche la mise en veille de l'écran.
/// Tout est restauré à la fermeture.
/// </summary>
internal static class SystemGuard
{
    private static STICKYKEYS _sticky;
    private static TOGGLEKEYS _toggle;
    private static FILTERKEYS _filter;
    private static bool _saved;

    private static Guid _scheme;
    private static uint _powerButtonAC, _powerButtonDC;
    private static bool _powerSaved;

    public static void Apply()
    {
        _sticky = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf<STICKYKEYS>() };
        _toggle = new TOGGLEKEYS { cbSize = (uint)Marshal.SizeOf<TOGGLEKEYS>() };
        _filter = new FILTERKEYS { cbSize = (uint)Marshal.SizeOf<FILTERKEYS>() };

        _saved = SystemParametersInfo(SPI_GETSTICKYKEYS, _sticky.cbSize, ref _sticky, 0)
               & SystemParametersInfo(SPI_GETTOGGLEKEYS, _toggle.cbSize, ref _toggle, 0)
               & SystemParametersInfo(SPI_GETFILTERKEYS, _filter.cbSize, ref _filter, 0);

        if (_saved)
        {
            // On ne touche aux raccourcis que si la fonction elle-même n'est pas activée
            if ((_sticky.dwFlags & SKF_STICKYKEYSON) == 0)
            {
                var s = _sticky;
                s.dwFlags &= ~(SKF_HOTKEYACTIVE | SKF_CONFIRMHOTKEY);
                SystemParametersInfo(SPI_SETSTICKYKEYS, s.cbSize, ref s, 0);
            }
            if ((_toggle.dwFlags & TKF_TOGGLEKEYSON) == 0)
            {
                var t = _toggle;
                t.dwFlags &= ~(TKF_HOTKEYACTIVE | TKF_CONFIRMHOTKEY);
                SystemParametersInfo(SPI_SETTOGGLEKEYS, t.cbSize, ref t, 0);
            }
            if ((_filter.dwFlags & FKF_FILTERKEYSON) == 0)
            {
                var f = _filter;
                f.dwFlags &= ~(FKF_HOTKEYACTIVE | FKF_CONFIRMHOTKEY);
                SystemParametersInfo(SPI_SETFILTERKEYS, f.cbSize, ref f, 0);
            }
        }

        DisablePowerButton();
        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
    }

    public static void Restore()
    {
        if (_saved)
        {
            SystemParametersInfo(SPI_SETSTICKYKEYS, _sticky.cbSize, ref _sticky, 0);
            SystemParametersInfo(SPI_SETTOGGLEKEYS, _toggle.cbSize, ref _toggle, 0);
            SystemParametersInfo(SPI_SETFILTERKEYS, _filter.cbSize, ref _filter, 0);
            _saved = false;
        }
        RestorePowerButton();
        SetThreadExecutionState(ES_CONTINUOUS);
    }

    // ------------------------------------------------------------------ Bouton d'alimentation

    /// <summary>
    /// Met l'action du bouton d'alimentation sur « Ne rien faire » dans le plan actif.
    /// Un appui court n'éteint donc plus le PC ; un appui long (coupure matérielle) reste possible.
    /// </summary>
    private static void DisablePowerButton()
    {
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr scheme) != 0 || scheme == IntPtr.Zero) return;
            try { _scheme = Marshal.PtrToStructure<Guid>(scheme); }
            finally { LocalFree(scheme); }

            var sub = GUID_SYSTEM_BUTTON_SUBGROUP;
            var setting = GUID_POWERBUTTON_ACTION;
            if (PowerReadACValueIndex(IntPtr.Zero, ref _scheme, ref sub, ref setting, out _powerButtonAC) != 0) return;
            if (PowerReadDCValueIndex(IntPtr.Zero, ref _scheme, ref sub, ref setting, out _powerButtonDC) != 0) return;
            if (_powerButtonAC == POWER_ACTION_NONE && _powerButtonDC == POWER_ACTION_NONE) return; // déjà inoffensif

            _powerSaved = true; // même si une écriture échoue, on tentera de remettre l'original
            PowerWriteACValueIndex(IntPtr.Zero, ref _scheme, ref sub, ref setting, POWER_ACTION_NONE);
            PowerWriteDCValueIndex(IntPtr.Zero, ref _scheme, ref sub, ref setting, POWER_ACTION_NONE);
            PowerSetActiveScheme(IntPtr.Zero, ref _scheme); // réactiver le plan applique les nouvelles valeurs
        }
        catch { }
    }

    private static void RestorePowerButton()
    {
        if (!_powerSaved) return;
        _powerSaved = false;
        try
        {
            var sub = GUID_SYSTEM_BUTTON_SUBGROUP;
            var setting = GUID_POWERBUTTON_ACTION;
            PowerWriteACValueIndex(IntPtr.Zero, ref _scheme, ref sub, ref setting, _powerButtonAC);
            PowerWriteDCValueIndex(IntPtr.Zero, ref _scheme, ref sub, ref setting, _powerButtonDC);
            PowerSetActiveScheme(IntPtr.Zero, ref _scheme);
        }
        catch { }
    }
}
