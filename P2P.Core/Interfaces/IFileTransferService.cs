using P2P.Core.Models;

namespace P2P.Core.Interfaces;

/// <summary>
/// Envoi / réception de fichiers par morceaux (membre 3).
/// Messages utilisés : FileStart, FileChunk, FileEnd.
/// </summary>
public interface IFileTransferService
{
    /// <summary>Progression : (nom du fichier, pourcentage 0 à 100).</summary>
    event Action<string, double>? ProgressChanged;

    /// <summary>Fichier entièrement reçu : (Id de l'expéditeur, chemin complet du fichier).</summary>
    event Action<string, string>? FileReceived;

    /// <summary>Dossier où les fichiers reçus sont enregistrés.</summary>
    string ReceiveFolder { get; set; }

    Task SendFileAsync(Peer peer, string filePath);

    /// <summary>
    /// À appeler par le NetworkService quand un message FileStart/FileChunk/FileEnd arrive.
    /// </summary>
    void HandleIncoming(ChatMessage message);
}
