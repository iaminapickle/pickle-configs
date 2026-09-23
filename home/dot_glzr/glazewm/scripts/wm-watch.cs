// Restarts GlazeWM when a monitor appears. Runs from startup_commands and
// watches WM_DISPLAYCHANGE, as focus-sync does for a monitor left without a
// bar.
//
// Only on appearing. Losing a monitor is handled -- GlazeWM migrates its
// workspaces onto the survivor -- and skipping that half costs no restart when
// a monitor drops for power-save. Gaining one isn't: `keep_alive: true` kept
// those workspaces alive where they landed, so nothing is left to activate on
// the returning monitor and it comes up empty. Every command that would repair
// it then fails with "No displayed workspace", leaving only a restart.
//
// wm-refresh.exe does that restart, the same binary lwin+alt+m runs: a
// separate process so a stale watcher can never restart anything by accident,
// and because wm-refresh kills focus-sync, which rules out hosting this there.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

class WmWatch
{
    const uint WmDisplayChange = 0x007E;
    const uint WsExNoActivate = 0x08000000;
    const uint WsExToolWindow = 0x00000080;
    const string WatcherClass = "WmWatchDisplayWatcher";

    // A switch emits a burst of WM_DISPLAYCHANGE and the monitors keep moving
    // for a moment after the last one.
    const int DisplaySettleMs = 1500;
    const int DisplayPollMs = 250;
    // If restarting keeps not helping, it is a bug and not a KVM switch.
    const int MaxRefreshes = 2;
    const int RefreshWindowMs = 600000;

    delegate IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASS
    {
        public uint style;
        public WindowProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ushort RegisterClass(ref WNDCLASS wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu,
        IntPtr instance, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetMessage(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr DispatchMessage(ref MSG msg);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string name);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    static bool dryRun;
    static Mutex single;

    // A field, not a local: the GC would collect the delegate while Windows
    // still held the pointer the class was registered with.
    static WindowProc watcherProc;
    static long displayChangedAt;

    [STAThread]
    static void Main(string[] args)
    {
        dryRun = args != null && Array.IndexOf(args, "--dry-run") >= 0;

        // A dry run takes its own name, so a forgotten one can't squat the
        // mutex and keep startup_commands' real watcher from ever running.
        bool owned;
        single = new Mutex(true, dryRun ? @"Local\wm-watch-dry" : @"Local\wm-watch", out owned);
        if (!owned) return;

        Log("started" + (dryRun ? " (dry run)" : "") + ", " + MonitorCount() + " monitors");

        var worker = new Thread(RefreshLoop);
        worker.IsBackground = true;
        worker.Start();

        try { WatchDisplays(); }
        catch (Exception ex) { Log(ex.ToString()); }
    }

    static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "wm-watch.log"),
                DateTime.Now + ": " + message + Environment.NewLine);
        }
        catch { }
    }

    // WM_DISPLAYCHANGE is a broadcast, and broadcasts only reach top-level
    // windows, so this one is ordinary and merely never shown -- a
    // message-only window would never hear it.
    static void WatchDisplays()
    {
        watcherProc = WatcherWndProc;

        var wndClass = new WNDCLASS
        {
            lpfnWndProc = watcherProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = WatcherClass,
        };

        if (RegisterClass(ref wndClass) == 0)
        {
            Log("could not register display watcher: " + Marshal.GetLastWin32Error());
            return;
        }

        // WS_EX_NOACTIVATE keeps it out of the focus order, as in focus-sync.
        IntPtr hwnd = CreateWindowEx(WsExNoActivate | WsExToolWindow, WatcherClass, WatcherClass,
                                     0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero,
                                     GetModuleHandle(null), IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
        {
            Log("could not create display watcher: " + Marshal.GetLastWin32Error());
            return;
        }

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    static IntPtr WatcherWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmDisplayChange)
            Interlocked.Exchange(ref displayChangedAt, DateTime.UtcNow.Ticks);

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    // The work is done here rather than in the window procedure, which must
    // not block the pump.
    static void RefreshLoop()
    {
        int known = MonitorCount();
        var refreshes = new Queue<DateTime>();

        while (true)
        {
            Thread.Sleep(DisplayPollMs);

            long at = Interlocked.Read(ref displayChangedAt);
            if (at == 0) continue;
            if ((DateTime.UtcNow - new DateTime(at)).TotalMilliseconds < DisplaySettleMs) continue;

            // Losing this to a newer message is the point: that one stays
            // pending and gets its own full settle.
            if (Interlocked.CompareExchange(ref displayChangedAt, 0, at) != at) continue;

            int now = MonitorCount();
            if (now == known) continue;

            bool gained = now > known;
            Log((gained ? "gained" : "lost") + " a monitor: " + known + " -> " + now);
            known = now;
            if (!gained) continue;

            if (dryRun) { Log("WOULD REFRESH"); continue; }

            while (refreshes.Count > 0 &&
                   (DateTime.Now - refreshes.Peek()).TotalMilliseconds > RefreshWindowMs)
                refreshes.Dequeue();
            if (refreshes.Count >= MaxRefreshes)
            {
                Log("refreshed " + MaxRefreshes + " times already, standing down");
                continue;
            }

            refreshes.Enqueue(DateTime.Now);
            RunRefresh();

            // The restart churns windows for a moment; don't read a stale
            // message left over from it.
            Interlocked.Exchange(ref displayChangedAt, 0);
            known = MonitorCount();
        }
    }

    static int MonitorCount()
    {
        int count = 0;

        MonitorEnumProc tally = delegate(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data)
        {
            count++;
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, tally, IntPtr.Zero);

        return count;
    }

    static void RunRefresh()
    {
        try
        {
            var exe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @".glzr\glazewm\scripts\wm-refresh.exe");
            if (!File.Exists(exe)) { Log("wm-refresh.exe not found: " + exe); return; }

            Log("restarting GlazeWM");
            var psi = new ProcessStartInfo(exe);
            psi.UseShellExecute = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            var p = Process.Start(psi);
            if (p != null) p.WaitForExit(60000);
        }
        catch (Exception ex) { Log(ex.ToString()); }
    }
}
