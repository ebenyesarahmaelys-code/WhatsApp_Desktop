using P2P.App.Helpers;
using P2P.Core;
using P2P.Core.Interfaces;
using P2P.Core.Models;

namespace P2P.App.Services;

/// <summary>
/// FAUX réseau pour développer l'interface sans attendre le membre 2 et le membre 3.
/// Simule des pairs qui apparaissent, des accusés de réception et des réponses automatiques.
/// Les événements sont volontairement déclenchés sur un autre thread, comme le vrai réseau.
/// </summary>
public class FakeNetworkService : INetworkService
{
    private readonly List<Peer> _fakePeers = new()
    {
        new Peer { Id = "fake-alain", Pseudo = "Alain K.", Ip = "192.168.1.20", Port = 5000, IsOnline = true },
        new Peer { Id = "fake-marie", Pseudo = "Marie S.", Ip = "192.168.1.31", Port = 5000, IsOnline = true, Status = "Occupée" },
        new Peer { Id = "fake-junior", Pseudo = "Junior T.", Ip = "192.168.1.44", Port = 5000, IsOnline = true },
        new Peer { Id = "fake-paul", Pseudo = "Paul A.", Ip = "192.168.1.57", Port = 5000, IsOnline = true },
    };

    private System.Threading.Timer? _timer;
    private bool _paulOnline = true;

    public Peer LocalPeer { get; private set; } = new();

    public event Action<Peer>? PeerDiscovered;
    public event Action<Peer>? PeerLost;
    public event Action<ChatMessage>? MessageReceived;

    public void Start(string pseudo, int tcpPort)
    {
        LocalPeer = new Peer
        {
            Pseudo = pseudo,
            Port = tcpPort,
            Ip = IpHelper.GetLocalIp(),
            IsOnline = true
        };

        // Les pairs "apparaissent" un par un, comme avec la découverte UDP
        Task.Run(async () =>
        {
            foreach (var p in _fakePeers)
            {
                await Task.Delay(400);
                PeerDiscovered?.Invoke(p);
            }
        });

        // Paul passe en ligne / hors ligne toutes les 15 s pour tester le statut
        _timer = new System.Threading.Timer(_ =>
        {
            var paul = _fakePeers[3];
            _paulOnline = !_paulOnline;
            paul.IsOnline = _paulOnline;
            if (_paulOnline) PeerDiscovered?.Invoke(paul);
            else PeerLost?.Invoke(paul);
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

        // Un message entrant de Marie au bout de 5 s pour tester le badge "non lu"
        Task.Run(async () =>
        {
            await Task.Delay(5000);
            MessageReceived?.Invoke(new ChatMessage
            {
                FromId = "fake-marie",
                FromPseudo = "Marie S.",
                ToId = LocalPeer.Id,
                Content = "Salut ! Le module fichiers avance bien 👍"
            });
        });
    }

    public void Stop() => _timer?.Dispose();

    public async Task SendMessageAsync(Peer peer, ChatMessage message)
    {
        if (!peer.IsOnline)
            throw new InvalidOperationException($"{peer.Pseudo} est hors ligne.");

        await Task.Delay(150); // "temps réseau"

        if (message.Type != MessageType.Text) return;

        _ = Task.Run(async () =>
        {
            // Accusé de réception -> ✓✓
            await Task.Delay(500);
            MessageReceived?.Invoke(new ChatMessage
            {
                Type = MessageType.Ack,
                FromId = peer.Id,
                ToId = LocalPeer.Id,
                Content = message.Id
            });

            // Réponse automatique
            await Task.Delay(1500);
            MessageReceived?.Invoke(new ChatMessage
            {
                FromId = peer.Id,
                FromPseudo = peer.Pseudo,
                ToId = LocalPeer.Id,
                Content = $"Bien reçu : « {message.Content} »"
            });
        });
    }

    public async Task<Peer?> ConnectManuallyAsync(string ip, int port)
    {
        await Task.Delay(800);
        var peer = new Peer
        {
            Id = $"manual-{ip}:{port}",
            Pseudo = $"Pair {ip}",
            Ip = ip,
            Port = port,
            IsOnline = true
        };
        _fakePeers.Add(peer);
        return peer;
    }
}
