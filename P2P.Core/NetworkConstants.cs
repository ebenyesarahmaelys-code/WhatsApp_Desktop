namespace P2P.Core;

/// <summary>
/// Valeurs partagées par tout le monde. Ne pas mettre de chiffres "en dur" ailleurs.
/// </summary>
public static class NetworkConstants
{
    /// <summary>Port TCP par défaut pour les messages et les fichiers.</summary>
    public const int DefaultTcpPort = 5000;

    /// <summary>Port UDP de découverte (broadcast).</summary>
    public const int DiscoveryUdpPort = 5001;

    /// <summary>Intervalle d'annonce UDP.</summary>
    public static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(3);

    /// <summary>Un pair est considéré hors ligne s'il ne s'annonce plus pendant ce délai.</summary>
    public static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Taille d'un morceau de fichier.</summary>
    public const int FileChunkSize = 64 * 1024;

    /// <summary>Délai max pour se connecter à un pair.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
}
