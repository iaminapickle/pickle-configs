// Background daemon: forces a real Win32 foreground-window change onto an
// empty monitor's desktop whenever GlazeWM's focus lands there.
//
// Why this exists: Windows only allows a genuine input event (click/keypress)
// to change the real foreground window. Hovering the mouse (focus_follows_cursor)
// updates GlazeWM's own internal focus bookkeeping, but NOT the real Win32
// foreground window when the target workspace is empty (nothing there to
// legitimately receive it). Anything that relies on real foreground/focus
// state to decide where a newly-launched app's window should go — which is
// effectively all of them, including GlazeWM's own new-window placement —
// then gets it wrong. This daemon subscribes to GlazeWM's focus_changed
// event over its WebSocket IPC (127.0.0.1:6123) and, whenever focus lands on
// an empty workspace (type=="workspace", no children), moves an invisible
// 1x1 helper window onto that monitor and forces it to become the real
// foreground window (using the standard Alt-keypress trick to bypass
// Windows' foreground-lock timer), so subsequent launches land correctly.
//
// Runs persistently, started via general.startup_commands in config.yaml.
// The helper window itself is ignored by GlazeWM via a window_rule matching
// its title (see config.yaml).
using System;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class FocusSync
{
    const string HelperTitle = "GlazeWMFocusSyncHelper";

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    const byte VK_MENU = 0x12;
    const uint KEYEVENTF_KEYUP = 0x0002;

    class HelperForm : Form
    {
        public HelperForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new System.Drawing.Size(1, 1);
            Opacity = 0;
            Text = HelperTitle;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public void ForceForegroundAt(double x, double y)
        {
            Location = new System.Drawing.Point((int)x, (int)y);
            if (!Visible)
                Show();
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            SetForegroundWindow(Handle);
        }
    }

    static HelperForm form;

    [STAThread]
    static void Main()
    {
        form = new HelperForm();
        form.Load += (s, e) =>
        {
            var thread = new Thread(ListenLoop);
            thread.IsBackground = true;
            thread.Start();
        };
        Application.Run(form);
    }

    static void ListenLoop()
    {
        while (true)
        {
            try
            {
                RunOnce();
            }
            catch (Exception ex)
            {
                Log("connection error, retrying in 3s: " + ex.Message);
            }
            Thread.Sleep(3000);
        }
    }

    static void RunOnce()
    {
        using (var ws = new ClientWebSocket())
        {
            ws.ConnectAsync(new Uri("ws://127.0.0.1:6123"), CancellationToken.None).GetAwaiter().GetResult();
            Send(ws, "sub -e focus_changed");
            Receive(ws); // ack

            while (ws.State == WebSocketState.Open)
            {
                string json = Receive(ws);
                HandleEvent(json);
            }
        }
    }

    static void HandleEvent(string json)
    {
        var serializer = new JavaScriptSerializer();
        var root = (System.Collections.Generic.Dictionary<string, object>)serializer.DeserializeObject(json);
        object dataObj;
        if (!root.TryGetValue("data", out dataObj) || dataObj == null) return;
        var data = (System.Collections.Generic.Dictionary<string, object>)dataObj;

        object containerObj;
        if (!data.TryGetValue("focusedContainer", out containerObj) || containerObj == null) return;
        var container = (System.Collections.Generic.Dictionary<string, object>)containerObj;

        string type = container["type"] as string;
        if (type != "workspace") return;

        var children = container["children"] as object[];
        if (children != null && children.Length > 0) return; // has windows, real focus already correct

        double x = Convert.ToDouble(container["x"]);
        double y = Convert.ToDouble(container["y"]);
        double width = Convert.ToDouble(container["width"]);
        double height = Convert.ToDouble(container["height"]);
        double targetX = x + width / 2;
        double targetY = y + height / 2;

        form.BeginInvoke((MethodInvoker)delegate
        {
            form.ForceForegroundAt(targetX, targetY);
        });
    }

    static void Send(ClientWebSocket ws, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
    }

    static string Receive(ClientWebSocket ws)
    {
        var buffer = new byte[131072];
        var ms = new System.IO.MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).GetAwaiter().GetResult();
            if (result.MessageType == WebSocketMessageType.Close)
                throw new Exception("socket closed by server");
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
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
