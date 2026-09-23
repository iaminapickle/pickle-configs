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
using System.Text;
using System.Threading;

class WmRefresh
{
    const string Endpoint = "ws://127.0.0.1:6123";
    const string Fallback = @"C:\Program Files\glzr.io\GlazeWM\glazewm.exe";
    const string ZebarFallback = @"C:\Program Files\glzr.io\Zebar\zebar.exe";

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
        KillAll("zebar");
        KillAll("focus-sync");
        Thread.Sleep(600);

        if (exe == null || !File.Exists(exe)) exe = Fallback;
        if (!File.Exists(exe)) { Log("glazewm.exe not found: " + exe); return; }

        var psi = new ProcessStartInfo(exe, "start");
        psi.UseShellExecute = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        Process.Start(psi);

        // startup_commands fire before the monitors are registered, so that
        // Zebar can miss one. Replace it once the IPC answers.
        if (!WaitIpcReady(15000)) Log("IPC did not answer; replacing Zebar anyway");
        RestartZebar(zebarExe);
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
