// Windowless directional focus for GlazeWM.
// Usage: focus-dir.exe <left|right|up|down>
//
// `focus --direction` resolves from the focused container and does nothing at
// all when that is a minimized window, so the arrow keys go dead on a
// workspace whose windows are all minimized. GlazeWM resolves from whatever
// `--id` names, so the workspace is passed instead in that case.
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

class FocusDir
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
                    System.IO.Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "focus-dir-error.log"),
                    DateTime.Now + ": " + ex + Environment.NewLine);
            }
            catch { }
        }
    }

    static void Run(string[] args)
    {
        if (args.Length < 1) return;
        string direction = args[0].ToLowerInvariant();
        if (direction != "left" && direction != "right" && direction != "up" && direction != "down")
            return;

        using (var ws = new ClientWebSocket())
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            ws.ConnectAsync(new Uri("ws://127.0.0.1:6123"), cts.Token).GetAwaiter().GetResult();

            string stuck = MinimizedFocus(ws, cts.Token);
            SendReceive(ws, stuck == null
                ? "command focus --direction " + direction
                : "command --id " + stuck + " focus --direction " + direction, cts.Token);

            try { ws.Abort(); } catch { }
        }
    }

    // Workspace holding focus through a minimized window, else null.
    static string MinimizedFocus(ClientWebSocket ws, CancellationToken token)
    {
        var focused = Field(Field(Parse(SendReceive(ws, "query focused", token)), "data"), "focused");
        if (Str(Field(focused, "state"), "type") != "minimized") return null;

        string windowId = Str(focused, "id");
        var workspaces = Arr(Field(Parse(SendReceive(ws, "query workspaces", token)), "data"), "workspaces");
        if (workspaces == null || windowId == null) return null;

        foreach (var workspaceObj in workspaces)
        {
            var workspace = workspaceObj as Dictionary<string, object>;
            if (ContainsId(Arr(workspace, "children"), windowId)) return Str(workspace, "id");
        }

        return null;
    }

    static bool ContainsId(object[] children, string id)
    {
        if (children == null) return false;

        foreach (var childObj in children)
        {
            var child = childObj as Dictionary<string, object>;
            if (child == null) continue;
            if (Str(child, "id") == id || ContainsId(Arr(child, "children"), id)) return true;
        }

        return false;
    }

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
