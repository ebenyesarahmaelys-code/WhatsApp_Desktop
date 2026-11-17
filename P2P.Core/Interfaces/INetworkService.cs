using P2P.Core.Models;

namespace P2P.Core.Interfaces;

/// <summary>
/// Réseau complet : découverte UDP + messages TCP.
/// Implémenté par NetworkService (membre 2, avec UdpDiscovery du membre 3).
/// ATTENTION : les événements sont déclenchés sur un thread réseau, pas sur le thread de l'interface.
/// </summary>
public interface INetworkService
{
    /// <summary>Moi-même (Id, pseudo, IP, port). Disponible après Start().</summary>
    Peer LocalPeer { get; }

    /// <summary>Un pair vient d'apparaître (ou a été mis à jour).</summary>
    event Action<Peer>? PeerDiscovered;

    /// <summary>Un pair ne s'annonce plus depuis NetworkConstants.PeerTimeout.</summary>
    event Action<Peer>? PeerLost;

    /// <summary>Un message est arrivé (Text, Ack, Profile, Hello...).</summary>
    event Action<ChatMessage>? MessageReceived;

    /// <summary>Démarre le serveur TCP et la découverte UDP.</summary>
    void Start(string pseudo, int tcpPort);

    /// <summary>Arrête tout proprement (à la fermeture de l'appli).</summary>
    void Stop();

    /// <summary>Envoie un message à un pair. Lève une exception si le pair est injoignable.</summary>
    Task SendMessageAsync(Peer peer, ChatMessage message);

    /// <summary>
    /// Connexion manuelle par IP (fenêtre "Ajouter un pair").
    /// Envoie un message Hello et retourne le pair, ou null si injoignable.
    /// </summary>
    Task<Peer?> ConnectManuallyAsync(string ip, int port);
}
