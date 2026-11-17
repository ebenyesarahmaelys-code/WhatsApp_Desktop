using P2P.Core.Models;

namespace P2P.App.ViewModels;

public enum DeliveryState { Sending, Sent, Delivered, Failed }

/// <summary>Une bulle de message dans la conversation.</summary>
public class MessageViewModel : ViewModelBase
{
    private DeliveryState _state;
    private double _progress;
    private string _fileInfo = "";

    public MessageViewModel(ChatMessage message, bool isMine)
    {
        Message = message;
        IsMine = isMine;
        _state = isMine ? DeliveryState.Sending : DeliveryState.Delivered;
    }

    public ChatMessage Message { get; }
    public string Id => Message.Id;
    public bool IsMine { get; }
    public string Content => Message.Content;
    public string Time => Message.Timestamp.ToString("HH:mm");

    // --- Fichiers ---
    public bool IsFile => Message.FileName != null;
    public string? FileName => Message.FileName;
    public string FileExtension =>
        string.IsNullOrEmpty(FileName) ? "" : System.IO.Path.GetExtension(FileName).TrimStart('.').ToUpper();

    public double Progress
    {
        get => _progress;
        set { if (SetProperty(ref _progress, value)) OnPropertyChanged(nameof(IsTransferring)); }
    }

    public bool IsTransferring => IsFile && Progress < 100;

    public string FileInfo { get => _fileInfo; set => SetProperty(ref _fileInfo, value); }

    /// <summary>Chemin local du fichier (pour l'ouvrir au double-clic).</summary>
    public string? LocalPath { get; set; }

    // --- Statut d'envoi ---
    public DeliveryState State
    {
        get => _state;
        set { if (SetProperty(ref _state, value)) OnPropertyChanged(nameof(StateIcon)); }
    }

    public string StateIcon => !IsMine ? "" : State switch
    {
        DeliveryState.Sending => "🕓",
        DeliveryState.Sent => "✓",
        DeliveryState.Delivered => "✓✓",
        DeliveryState.Failed => "⚠",
        _ => ""
    };
}
