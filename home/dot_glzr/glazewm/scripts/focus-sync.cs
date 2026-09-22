// Three GlazeWM focus fixes for a multi-monitor setup, over its WebSocket IPC
// (127.0.0.1:6123). Runs persistently via general.startup_commands.
//
// 1. A click on bare desktop makes Program Manager the foreground window,
//    which GlazeWM doesn't manage, so its focus stays on the other monitor and
//    lwin+N keeps addressing that one. Foreground is polled and the monitor
//    under the pointer is focused.
//
// 2. An empty workspace holds no real foreground, so a closing overlay like
//    the Command Palette hands it back to the last real window on the other
//    monitor and a window opened in that moment lands there. That flick is
//    tens of milliseconds, so the workspace that held focus for StableMs wins
//    and the window is moved back.
//
// 3. GlazeWM can focus a minimized window, which nothing visibly focuses and
//    `focus --direction` won't move off. Focus is handed to a window on that
//    workspace that isn't minimized.
//
// Foreground is only read, never set: an earlier version forced it onto an
// invisible helper window, which denied GlazeWM the rights it needs to focus
// a hovered window.
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

class FocusSync
{
    const string Endpoint = "ws://127.0.0.1:6123";
    const int PollMs = 100;
    const int StableMs = 125;
    const int SettleMs = 120;
    const int FocusAttempts = 3;
    const int HistoryLimit = 16;

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder buffer, int maxCount);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT point);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int X;
        public int Y;
    }

    class Focus
    {
        public string Id;
        public DateTime At;
    }

    static readonly List<Focus> history = new List<Focus>();

    static void Main()
    {
        var events = new Thread(EventLoop);
        events.IsBackground = true;
        events.Start();

        PollForeground();
    }

    // ---- 1. desktop clicks ------------------------------------------------

    static void PollForeground()
    {
        // Only the transition into the desktop matters, so holding it there
        // doesn't re-issue the command ten times a second.
        IntPtr last = IntPtr.Zero;

        while (true)
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                if (foreground != last)
                {
                    last = foreground;
                    if (IsDesktop(foreground))
                        FocusMonitorUnderCursor();
                }
            }
            catch (Exception ex)
            {
                Log("poll error: " + ex.Message);
            }

            Thread.Sleep(PollMs);
        }
    }

    // Progman is the desktop; WorkerW hosts the wallpaper and can take
    // foreground instead.
    static bool IsDesktop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        var buffer = new StringBuilder(256);
        GetClassName(hwnd, buffer, buffer.Capacity);
        string className = buffer.ToString();

        return className == "Progman" || className == "WorkerW";
    }

    static void FocusMonitorUnderCursor()
    {
        POINT cursor;
        if (!GetCursorPos(out cursor)) return;

        using (var session = Connect())
        {
            var monitors = Arr(Field(Parse(session.Ask("query monitors")), "data"), "monitors");
            if (monitors == null) return;

            // Sorted left to right, the convention workspace-nav.exe uses for
            // which index GlazeWM means by a monitor.
            var sorted = new List<Dictionary<string, object>>();
            foreach (var monitorObj in monitors)
            {
                var monitor = monitorObj as Dictionary<string, object>;
                if (monitor != null) sorted.Add(monitor);
            }
            sorted.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b)
            {
                return Num(a, "x").CompareTo(Num(b, "x"));
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                if (!Contains(sorted[i], cursor)) continue;
                if (Bool(sorted[i], "hasFocus")) return; // already the focused one
                session.Ask("command focus --monitor " + i);
                return;
            }
        }
    }

    static bool Contains(Dictionary<string, object> monitor, POINT point)
    {
        double x = Num(monitor, "x");
        double y = Num(monitor, "y");

        return point.X >= x && point.X < x + Num(monitor, "width")
            && point.Y >= y && point.Y < y + Num(monitor, "height");
    }

    // ---- 2. misplaced new windows -----------------------------------------

    static void EventLoop()
    {
        while (true)
        {
            try
            {
                Subscribe();
            }
            catch (Exception ex)
            {
                Log("connection error, retrying in 3s: " + ex.Message);
            }

            Thread.Sleep(3000);
        }
    }

    static void Subscribe()
    {
        using (var session = Connect())
        {
            session.Ask("sub -e focus_changed window_managed");
            SeedHistory();

            while (session.Open)
                HandleEvent(session.Receive());
        }
    }

    // No history until the first focus_changed, so seed it as long-stable:
    // it's the only clue to where the user is.
    static void SeedHistory()
    {
        try
        {
            using (var session = Connect())
            {
                var workspaces = Arr(Field(Parse(session.Ask("query workspaces")), "data"), "workspaces");
                if (workspaces == null) return;

                foreach (var workspaceObj in workspaces)
                {
                    var workspace = workspaceObj as Dictionary<string, object>;
                    if (workspace == null || !Bool(workspace, "hasFocus")) continue;

                    lock (history)
                    {
                        history.Clear();
                        history.Add(new Focus
                        {
                            Id = Str(workspace, "id"),
                            At = DateTime.UtcNow - TimeSpan.FromSeconds(1),
                        });
                    }
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log("could not seed focus: " + ex.Message);
        }
    }

    static void HandleEvent(string json)
    {
        var data = Field(Parse(json), "data");
        if (data == null) return;

        string eventType = Str(data, "eventType");
        var container = Field(data, eventType == "window_managed" ? "managedWindow" : "focusedContainer");
        if (container == null) return;

        if (eventType == "focus_changed")
        {
            RecordFocus(Str(container, "id"));
            if (IsMinimized(container)) Unstick(Str(container, "id"));
        }
        else if (eventType == "window_managed")
        {
            // A window that took focus is one the user just opened; leave
            // anything that appeared in the background where it is.
            if (Bool(container, "hasFocus"))
                Reposition(Str(container, "id"));
        }
    }

    static void RecordFocus(string id)
    {
        if (id == null) return;

        lock (history)
        {
            history.Add(new Focus { Id = id, At = DateTime.UtcNow });
            if (history.Count > HistoryLimit) history.RemoveAt(0);
        }
    }

    // Newest id that isn't the new window and has held focus for StableMs,
    // falling back to the newest other id when focus has only just settled.
    static string StableFocus(string windowId)
    {
        var now = DateTime.UtcNow;

        lock (history)
        {
            string newest = null;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Id == windowId) continue;
                if (newest == null) newest = history[i].Id;
                if ((now - history[i].At).TotalMilliseconds >= StableMs)
                    return history[i].Id;
            }

            return newest;
        }
    }

    static void Reposition(string windowId)
    {
        if (windowId == null) return;

        // A second connection: the event socket is mid-receive, so replies
        // there would have to be matched to requests by hand.
        using (var session = Connect())
        {
            var workspaces = Arr(Field(Parse(session.Ask("query workspaces")), "data"), "workspaces");
            if (workspaces == null) return;

            string landed = WorkspaceOf(workspaces, windowId);
            if (landed == null) return;

            string intended = WorkspaceOf(workspaces, StableFocus(windowId));
            if (intended == null || intended == landed) return;

            // Focus the workspace first: moving a focused window hands focus
            // back to the workspace it left, and the border effect follows
            // that instead of the window.
            session.Ask("command focus --workspace " + intended);

            // By id, since `move` acts on whatever holds focus and
            // `focus --workspace` lands on that workspace's last focused.
            session.Ask("command --id " + windowId + " move --workspace " + intended);
            FocusWindow(session, windowId);
        }
    }

    // The move's own focus change can land after this one, so verify and
    // retry.
    static void FocusWindow(Session session, string windowId)
    {
        for (int attempt = 0; attempt < FocusAttempts; attempt++)
        {
            session.Ask("command focus --container-id " + windowId);
            Thread.Sleep(SettleMs);
            if (IsFocused(session, windowId, false)) return;
        }

        Log("gave up focusing " + windowId + " after " + FocusAttempts + " attempts");
    }

    static void Unstick(string windowId)
    {
        if (windowId == null) return;

        // GlazeWM moves focus off a window it minimizes, so only a focus
        // that stays put is stuck.
        Thread.Sleep(SettleMs);

        using (var session = Connect())
        {
            if (!IsFocused(session, windowId, true)) return;

            var workspaces = Arr(Field(Parse(session.Ask("query workspaces")), "data"), "workspaces");
            var workspace = WorkspaceContaining(workspaces, windowId);
            if (workspace == null) return;

            // Nothing to hand focus to when they're all minimized: the
            // focused container is the deepest in the focus order, so the
            // workspace bounces straight back here. focus-dir.exe keeps the
            // arrow keys working from there.
            string restored = FirstRestored(Arr(workspace, "children"));
            if (restored == null) return;

            session.Ask("command focus --container-id " + restored);
        }
    }

    // hasFocus is true for any window heading its workspace's focus order,
    // even when the workspace holds focus, so ask which container is focused.
    static bool IsFocused(Session session, string windowId, bool minimized)
    {
        var focused = Field(Field(Parse(session.Ask("query focused")), "data"), "focused");
        return Str(focused, "id") == windowId && (!minimized || IsMinimized(focused));
    }

    static bool IsMinimized(Dictionary<string, object> window)
    {
        return Str(Field(window, "state"), "type") == "minimized";
    }

    static string FirstRestored(object[] children)
    {
        if (children == null) return null;

        foreach (var childObj in children)
        {
            var child = childObj as Dictionary<string, object>;
            if (child == null) continue;

            if (Str(child, "type") == "window")
            {
                if (!IsMinimized(child)) return Str(child, "id");
            }
            else
            {
                var found = FirstRestored(Arr(child, "children"));
                if (found != null) return found;
            }
        }

        return null;
    }

    static Dictionary<string, object> WorkspaceContaining(object[] workspaces, string id)
    {
        if (workspaces == null || id == null) return null;

        foreach (var workspaceObj in workspaces)
        {
            var workspace = workspaceObj as Dictionary<string, object>;
            if (workspace == null) continue;
            if (ContainsId(Arr(workspace, "children"), id)) return workspace;
        }

        return null;
    }

    static string WorkspaceOf(object[] workspaces, string id)
    {
        if (id == null) return null;

        foreach (var workspaceObj in workspaces)
        {
            var workspace = workspaceObj as Dictionary<string, object>;
            if (workspace == null) continue;
            if (Str(workspace, "id") == id || ContainsId(Arr(workspace, "children"), id))
                return Str(workspace, "name");
        }

        return null;
    }

    static bool ContainsId(object[] children, string id)
    {
        return FindById(children, id) != null;
    }

    static Dictionary<string, object> FindById(object[] children, string id)
    {
        if (children == null || id == null) return null;

        foreach (var childObj in children)
        {
            var child = childObj as Dictionary<string, object>;
            if (child == null) continue;
            if (Str(child, "id") == id) return child;
            var found = FindById(Arr(child, "children"), id);
            if (found != null) return found;
        }

        return null;
    }

    // ---- IPC ---------------------------------------------------------------

    class Session : IDisposable
    {
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        public void Connect()
        {
            socket.ConnectAsync(new Uri(Endpoint), timeout.Token).GetAwaiter().GetResult();
        }

        public bool Open
        {
            get { return socket.State == WebSocketState.Open; }
        }

        // Commands and queries are request/response, so the reply is the next
        // message on a socket that isn't subscribed to anything.
        public string Ask(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token)
                  .GetAwaiter().GetResult();
            return Receive();
        }

        public string Receive()
        {
            var buffer = new byte[131072];
            var ms = new System.IO.MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                // Events arrive whenever GlazeWM has one, so a subscribed
                // socket must not time out waiting.
                result = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                               .GetAwaiter().GetResult();
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new Exception("socket closed by server");
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        public void Dispose()
        {
            try { socket.Abort(); } catch { }
            try { socket.Dispose(); } catch { }
        }
    }

    static Session Connect()
    {
        var session = new Session();
        session.Connect();
        return session;
    }

    // ---- JSON --------------------------------------------------------------

    static Dictionary<string, object> Parse(string json)
    {
        return new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
    }

    static Dictionary<string, object> Field(Dictionary<string, object> obj, string key)
    {
        if (obj == null) return null;
        object value;
        if (!obj.TryGetValue(key, out value)) return null;
        return value as Dictionary<string, object>;
    }

    static object[] Arr(Dictionary<string, object> obj, string key)
    {
        if (obj == null) return null;
        object value;
        if (!obj.TryGetValue(key, out value)) return null;
        return value as object[];
    }

    static string Str(Dictionary<string, object> obj, string key)
    {
        if (obj == null) return null;
        object value;
        if (!obj.TryGetValue(key, out value)) return null;
        return value as string;
    }

    static double Num(Dictionary<string, object> obj, string key)
    {
        if (obj == null) return 0;
        object value;
        if (!obj.TryGetValue(key, out value) || value == null) return 0;
        return Convert.ToDouble(value);
    }

    static bool Bool(Dictionary<string, object> obj, string key)
    {
        if (obj == null) return false;
        object value;
        if (!obj.TryGetValue(key, out value) || value == null) return false;
        return Convert.ToBoolean(value);
    }

    static void Log(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "focus-sync-error.log"),
                DateTime.Now + ": " + msg + Environment.NewLine);
        }
        catch { }
    }
}
