using System.Runtime.InteropServices;
using static TravailCommePapa.NativeMethods;

namespace TravailCommePapa;

/// <summary>
/// Désactive temporairement les raccourcis d'accessibilité qui ouvrent des popups
/// (Shift x5 = touches rémanentes, Maj droite 8 s = touches filtres, Verr.Num 5 s = touches bascules)
/// et empêche la mise en veille de l'écran. Tout est restauré à la fermeture.
/// </summary>
internal static class SystemGuard
{
    private static STICKYKEYS _sticky;
    private static TOGGLEKEYS _toggle;
    private static FILTERKEYS _filter;
    private static bool _saved;

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
        SetThreadExecutionState(ES_CONTINUOUS);
    }
}
