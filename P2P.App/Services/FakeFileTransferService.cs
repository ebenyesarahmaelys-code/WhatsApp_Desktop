using System.IO;
using P2P.Core.Interfaces;
using P2P.Core.Models;

namespace P2P.App.Services;

/// <summary>
/// FAUX transfert de fichiers : simule une progression de 0 à 100 %.
/// </summary>
public class FakeFileTransferService : IFileTransferService
{
    public event Action<string, double>? ProgressChanged;
    public event Action<string, string>? FileReceived;

    public string ReceiveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WhatsAppP2P", "Reçus");

    public async Task SendFileAsync(Peer peer, string filePath)
    {
        var name = Path.GetFileName(filePath);
        for (int pct = 0; pct <= 100; pct += 5)
        {
            await Task.Delay(120).ConfigureAwait(false); // thread réseau simulé
            ProgressChanged?.Invoke(name, pct);
        }
    }

    public void HandleIncoming(ChatMessage message)
    {
        // Rien à faire dans la version factice
    }
}
