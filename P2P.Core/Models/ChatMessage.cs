namespace P2P.Core.Models;

public enum MessageType
{
    /// <summary>Message texte classique.</summary>
    Text,
    /// <summary>Accusé de réception. Content = Id du message reçu.</summary>
    Ack,
    /// <summary>Présentation lors d'une connexion manuelle par IP (pseudo, port...).</summary>
    Hello,
    /// <summary>Envoi du profil (photo de profil en Base64 dans Content, statut...).</summary>
    Profile,
    /// <summary>Début d'un fichier : FileName + FileSize.</summary>
    FileStart,
    /// <summary>Un morceau du fichier en Base64 dans Content.</summary>
    FileChunk,
    /// <summary>Fin du fichier.</summary>
    FileEnd
}

/// <summary>
/// Tout ce qui circule entre deux pairs en TCP (sérialisé en JSON).
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MessageType Type { get; set; } = MessageType.Text;

    public string FromId { get; set; } = "";
    public string FromPseudo { get; set; } = "";
    public string ToId { get; set; } = "";

    /// <summary>Texte du message, ou morceau de fichier en Base64, ou Id du message acquitté.</summary>
    public string Content { get; set; } = "";

    public string? FileName { get; set; }
    public long FileSize { get; set; }

    /// <summary>Port TCP d'écoute de l'expéditeur (utile pour lui répondre).</summary>
    public int FromPort { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
