using System.Runtime.InteropServices;
using static TravailCommePapa.NativeMethods;

namespace TravailCommePapa;

/// <summary>
/// Hook clavier bas niveau : intercepte TOUTES les touches (Windows, Alt+Tab, Ctrl+Échap,
/// Alt+F4, touches multimédia...) avant Windows, les avale, et les transmet à l'application.
/// Seuls Ctrl+Alt+Suppr et Win+L restent gérés par Windows (impossible à bloquer par design).
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly LowLevelKeyboardProc _proc; // gardé en champ pour éviter le GC
    private IntPtr _hook;

    /// <summary>(touche, appuyée ?) — appelé sur le thread UI.</summary>
    public event Action<Keys, bool>? KeyEvent;

    public KeyboardHook()
    {
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Impossible d'installer le hook clavier (code " + Marshal.GetLastWin32Error() + ").");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool up = msg == WM_KEYUP || msg == WM_SYSKEYUP;
            if (down || up)
            {
                try { KeyEvent?.Invoke((Keys)info.vkCode, down); }
                catch { /* ne jamais laisser une exception remonter dans le hook */ }
            }
            return new IntPtr(1); // touche avalée : Windows ne la voit pas
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
