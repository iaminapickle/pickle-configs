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
using System.Globalization;

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
        var root = (Dictionary<string, object>)Json.Parse(json);
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
        var root = (Dictionary<string, object>)Json.Parse(json);
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

// Minimal JSON reader, replacing System.Web.Script.Serialization so the exe no
// longer loads System.Web.Extensions on every keypress. Produces the same shape
// JavaScriptSerializer.DeserializeObject did -- Dictionary<string, object> for
// objects, object[] for arrays, double for numbers -- so the callers above are
// unchanged. Assumes GlazeWM's well-formed responses; a malformed one throws and
// Main logs it.
class Json
{
    readonly string s;
    int i;

    Json(string text) { s = text; }

    public static object Parse(string text)
    {
        var p = new Json(text);
        p.Ws();
        return p.Value();
    }

    void Ws()
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
            i++;
    }

    object Value()
    {
        switch (s[i])
        {
            case '{': return ReadObject();
            case '[': return ReadArray();
            case '"': return ReadString();
            case 't': i += 4; return true;
            case 'f': i += 5; return false;
            case 'n': i += 4; return null;
            default: return ReadNumber();
        }
    }

    Dictionary<string, object> ReadObject()
    {
        var obj = new Dictionary<string, object>();
        i++; // '{'
        Ws();
        if (s[i] == '}') { i++; return obj; }

        while (true)
        {
            Ws();
            string key = ReadString();
            Ws();
            i++; // ':'
            Ws();
            obj[key] = Value();
            Ws();
            if (s[i++] == '}') return obj; // otherwise it was ','
        }
    }

    object[] ReadArray()
    {
        var list = new List<object>();
        i++; // '['
        Ws();
        if (s[i] == ']') { i++; return list.ToArray(); }

        while (true)
        {
            Ws();
            list.Add(Value());
            Ws();
            if (s[i++] == ']') return list.ToArray(); // otherwise it was ','
        }
    }

    string ReadString()
    {
        var sb = new StringBuilder();
        i++; // opening quote
        while (true)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }

            char escape = s[i++];
            switch (escape)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                    i += 4;
                    break;
            }
        }
    }

    object ReadNumber()
    {
        int start = i;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || (c >= '0' && c <= '9'))
                i++;
            else
                break;
        }
        return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
    }
}
