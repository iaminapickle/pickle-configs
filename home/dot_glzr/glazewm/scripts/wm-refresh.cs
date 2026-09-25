// Restarts GlazeWM after a monitor is plugged in or removed, bound to
// lwin+alt+m. One config covers both layouts, but a reload cannot fix the
// monitor/workspace mapping: GlazeWM strands the workspaces of a monitor that
// went away on the remaining one, and once a monitor has no displayed
// workspace both `move-workspace` and `focus --monitor` refuse to touch it
// ("No displayed workspace"). Only a restart rebuilds the mapping. focus-sync
// rides along on the config's shutdown/startup commands; Zebar is restarted
// explicitly, its bars being built from the monitor set it sees at launch.
using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class WmRefresh
{
    const string Endpoint = "ws://127.0.0.1:6123";
    const string Fallback = @"C:\Program Files\glzr.io\GlazeWM\glazewm.exe";
    const string ZebarFallback = @"C:\Program Files\glzr.io\Zebar\zebar.exe";

    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    static void Main()
    {
        try { Run(); }
        catch (Exception ex) { Log(ex.ToString()); }
    }

    static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "wm-refresh.log"),
                DateTime.Now + ": " + message + Environment.NewLine);
        }
        catch { }
    }

    static void Run()
    {
        string exe = null;
        foreach (var p in Process.GetProcessesByName("glazewm"))
        {
            try { if (exe == null) exe = p.MainModule.FileName; } catch { }
        }

        string zebarExe = null;
        foreach (var p in Process.GetProcessesByName("zebar"))
        {
            try { if (zebarExe == null) zebarExe = p.MainModule.FileName; } catch { }
        }

        // wm-exit over IPC rather than a kill, so shutdown_commands run and
        // Zebar and focus-sync don't survive into the new instance.
        if (!Exit()) KillAll("glazewm");

        if (!WaitGone("glazewm", 10000))
        {
            KillAll("glazewm");
            WaitGone("glazewm", 3000);
        }

        // Anything the shutdown commands missed would otherwise double up.
        // wm-watch is no longer launched at startup; kill any lingering instance
        // so an old one can't keep auto-restarting now that refresh is manual.
        KillAll("zebar");
        KillAll("focus-sync");
        KillAll("wm-watch");
        Thread.Sleep(600);

        if (exe == null || !File.Exists(exe)) exe = Fallback;
        if (!File.Exists(exe)) { Log("glazewm.exe not found: " + exe); return; }

        // Load the config matching the current monitor count before GlazeWM
        // reads it: single-monitor gets the native, fast workspace bindings,
        // two or more gets the per-monitor workspace-nav.exe bindings.
        SelectConfig();

        var psi = new ProcessStartInfo(exe, "start");
        psi.UseShellExecute = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(psi);

        // startup_commands fire before the monitors are registered, so that
        // Zebar can miss one. Replace it once the IPC answers.
        if (!WaitIpcReady(15000)) Log("IPC did not answer; replacing Zebar anyway");
        RestartZebar(zebarExe);
    }

    // Copy config.single.yaml (one monitor) or config.dual.yaml (two or more)
    // over the active config.yaml, so the relaunched GlazeWM reads the right
    // bindings. A missing source is logged and left alone rather than breaking
    // the restart.
    static void SelectConfig()
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @".glzr\glazewm");
            int count = MonitorCount();
            string source = Path.Combine(dir, count <= 1 ? "config.single.yaml" : "config.dual.yaml");
            string active = Path.Combine(dir, "config.yaml");

            if (!File.Exists(source)) { Log("config source not found: " + source); return; }
            File.Copy(source, active, true);
            Log("selected " + Path.GetFileName(source) + " for " + count + " monitor(s)");
        }
        catch (Exception ex) { Log("config select failed: " + ex); }
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

    static void RestartZebar(string zebarExe)
    {
        KillAll("zebar");
        Thread.Sleep(400);

        if (zebarExe == null || !File.Exists(zebarExe)) zebarExe = ZebarFallback;
        if (!File.Exists(zebarExe)) { Log("zebar.exe not found: " + zebarExe); return; }

        var psi = new ProcessStartInfo(zebarExe);
        psi.UseShellExecute = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(psi);
    }

    // A connect succeeds before the WM can serve state, so wait on a query.
    static bool WaitIpcReady(int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                using (var ws = new ClientWebSocket())
                {
                    var token = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;
                    ws.ConnectAsync(new Uri(Endpoint), token).GetAwaiter().GetResult();
                    var bytes = Encoding.UTF8.GetBytes("query monitors");
                    ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token)
                      .GetAwaiter().GetResult();
                    var buffer = new byte[16384];
                    ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).GetAwaiter().GetResult();
                    try { ws.Abort(); } catch { }
                    return true;
                }
            }
            catch { Thread.Sleep(300); }
        }
        return false;
    }

    static bool Exit()
    {
        try
        {
            using (var ws = new ClientWebSocket())
            {
                var token = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;
                ws.ConnectAsync(new Uri(Endpoint), token).GetAwaiter().GetResult();
                var bytes = Encoding.UTF8.GetBytes("command wm-exit");
                ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token)
                  .GetAwaiter().GetResult();
                Thread.Sleep(400);
                try { ws.Abort(); } catch { }
                return true;
            }
        }
        catch { return false; }
    }

    static void KillAll(string name)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
        }
        catch { }
    }

    static bool WaitGone(string name, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Process.GetProcessesByName(name).Length == 0) return true;
            Thread.Sleep(200);
        }
        return false;
    }
}
