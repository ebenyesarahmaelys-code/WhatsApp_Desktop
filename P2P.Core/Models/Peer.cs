namespace P2P.Core.Models;

/// <summary>
/// Un utilisateur de l'application sur le réseau (soi-même ou un contact).
/// </summary>
public class Peer
{
    /// <summary>Identifiant unique, généré une fois par instance (Guid).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Pseudo { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; } = NetworkConstants.DefaultTcpPort;

    /// <summary>Statut texte ("Disponible", "Occupé"...).</summary>
    public string Status { get; set; } = "Disponible";

    /// <summary>Photo de profil encodée en Base64 (null = initiales).</summary>
    public string? PhotoBase64 { get; set; }

    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.Now;

    public override string ToString() => $"{Pseudo} ({Ip}:{Port})";
}
