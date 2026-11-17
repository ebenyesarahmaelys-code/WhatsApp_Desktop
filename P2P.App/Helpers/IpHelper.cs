using System.Net;
using System.Net.Sockets;

namespace P2P.App.Helpers;

public static class IpHelper
{
    /// <summary>
    /// IP locale du PC (ex : 192.168.1.12). Astuce : on "connecte" un socket UDP vers
    /// une IP externe (aucun paquet n'est envoyé) pour savoir quelle carte réseau est utilisée.
    /// </summary>
    public static string GetLocalIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep) return ep.Address.ToString();
        }
        catch { /* pas de réseau : on essaie autrement */ }

        var ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        return ip?.ToString() ?? "127.0.0.1";
    }
}
