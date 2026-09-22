// Fast, windowless per-monitor workspace switcher for GlazeWM.
// Usage:
//   workspace-nav.exe <N 1-9> [-Move]   focus (or move+focus) workspace N on
//                                       whichever monitor is currently focused
//                                       (1-9 -> monitor 0, 11-19 -> monitor 1)
//   workspace-nav.exe next|prev [-Move] cycle to the next/prev workspace within
//                                       the current workspace's own decade
//                                       (1-9 wraps within itself, 11-19 wraps
//                                       within itself) so each monitor's
//                                       workspace set is fully isolated.
//
// Uses one direct WebSocket IPC connection (127.0.0.1:6123) rather than
// spawning glazewm-cli twice, which was the dominant cost.
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

class WorkspaceNav
{
    static void Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "workspace-nav-error.log"),
                    DateTime.Now + ": " + ex + Environment.NewLine);
            }
            catch { }
        }
    }

    static void Run(string[] args)
    {
        if (args.Length < 1) return;
        string mode = args[0].ToLowerInvariant();

        bool move = false;
        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-Move", StringComparison.OrdinalIgnoreCase))
                move = true;
        }

        using (var ws = new ClientWebSocket())
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            ws.ConnectAsync(new Uri("ws://127.0.0.1:6123"), cts.Token).GetAwaiter().GetResult();

            string workspaceName = (mode == "next" || mode == "prev")
                ? ResolveCycleTarget(ws, mode, cts.Token)
                : ResolveNumberedTarget(ws, mode, cts.Token);

            if (workspaceName == null)
                return;

            if (move)
                SendReceive(ws, "command move --workspace " + workspaceName, cts.Token);
            SendReceive(ws, "command focus --workspace " + workspaceName, cts.Token);

            try { ws.Abort(); } catch { }
        }
    }

    // lwin+N: focus workspace N on whichever monitor is currently focused.
    static string ResolveNumberedTarget(ClientWebSocket ws, string arg, CancellationToken token)
    {
        int n;
        if (!int.TryParse(arg, out n) || n < 1 || n > 9) return null;

        string json = SendReceive(ws, "query monitors", token);
        var serializer = new JavaScriptSerializer();
        var root = (Dictionary<string, object>)serializer.DeserializeObject(json);
        var data = (Dictionary<string, object>)root["data"];
        var monitors = (object[])data["monitors"];

        var sorted = new List<Dictionary<string, object>>();
        foreach (var m in monitors)
            sorted.Add((Dictionary<string, object>)m);
        sorted.Sort((a, b) => Convert.ToDouble(a["x"]).CompareTo(Convert.ToDouble(b["x"])));

        int focusedIndex = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            if (Convert.ToBoolean(sorted[i]["hasFocus"]))
            {
                focusedIndex = i;
                break;
            }
        }

        return focusedIndex == 0 ? n.ToString() : (n + 10 * focusedIndex).ToString();
    }

    // lwin+a / lwin+d: cycle within the current workspace's own decade
    // (1-9 <-> 1-9, 11-19 <-> 11-19), wrapping at each end.
    static string ResolveCycleTarget(ClientWebSocket ws, string direction, CancellationToken token)
    {
        string json = SendReceive(ws, "query workspaces", token);
        var serializer = new JavaScriptSerializer();
        var root = (Dictionary<string, object>)serializer.DeserializeObject(json);
        var data = (Dictionary<string, object>)root["data"];
        var workspaces = (object[])data["workspaces"];

        string currentName = null;
        foreach (var wObj in workspaces)
        {
            var w = (Dictionary<string, object>)wObj;
            if (Convert.ToBoolean(w["hasFocus"]))
            {
                currentName = (string)w["name"];
                break;
            }
        }
        if (currentName == null) return null;

        int currentNum;
        if (!int.TryParse(currentName, out currentNum)) return null;

        int decade = ((currentNum - 1) / 10) * 10;
        int n = currentNum - decade;

        int newN = direction == "next"
            ? (n == 9 ? 1 : n + 1)
            : (n == 1 ? 9 : n - 1);

        return (decade + newN).ToString();
    }

    static string SendReceive(ClientWebSocket ws, string message, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).GetAwaiter().GetResult();

        var buffer = new byte[131072];
        var ms = new System.IO.MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).GetAwaiter().GetResult();
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
