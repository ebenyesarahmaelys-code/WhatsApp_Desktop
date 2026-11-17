using System.Collections.ObjectModel;
using System.Windows.Media;
using P2P.Core.Models;

namespace P2P.App.ViewModels;

/// <summary>Un contact dans la liste de gauche, avec sa conversation.</summary>
public class PeerViewModel : ViewModelBase
{
    private static readonly string[] Palette = { "#3B6FD1", "#B4541A", "#7A4FB8", "#0F6E56", "#B83A63", "#2F7F8F" };

    private bool _isOnline;
    private int _unreadCount;
    private string _lastMessage = "";
    private string _lastTime = "";
    private string _status = "";

    public PeerViewModel(Peer peer)
    {
        Peer = peer;
        _isOnline = peer.IsOnline;
        _status = peer.Status;

        // Couleur d'avatar stable selon l'Id
        int index = Math.Abs(peer.Id.GetHashCode()) % Palette.Length;
        AvatarBrush = (SolidColorBrush)new BrushConverter().ConvertFrom(Palette[index])!;
        AvatarBrush.Freeze();
    }

    public Peer Peer { get; private set; }
    public string Id => Peer.Id;
    public string Pseudo => Peer.Pseudo;
    public string Address => $"{Peer.Ip}:{Peer.Port}";
    public Brush AvatarBrush { get; }

    public string Initials
    {
        get
        {
            var parts = Pseudo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        }
    }

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    public bool IsOnline
    {
        get => _isOnline;
        set { if (SetProperty(ref _isOnline, value)) OnPropertyChanged(nameof(OnlineText)); }
    }

    public string OnlineText => IsOnline ? $"En ligne · {Address}" : $"Hors ligne · {Address}";

    public string Status { get => _status; set => SetProperty(ref _status, value); }

    public int UnreadCount
    {
        get => _unreadCount;
        set { if (SetProperty(ref _unreadCount, value)) OnPropertyChanged(nameof(HasUnread)); }
    }

    public bool HasUnread => UnreadCount > 0;

    public string LastMessage { get => _lastMessage; set => SetProperty(ref _lastMessage, value); }
    public string LastTime { get => _lastTime; set => SetProperty(ref _lastTime, value); }

    /// <summary>Met à jour les infos quand le pair se ré-annonce (IP ou pseudo changé...).</summary>
    public void Update(Peer peer)
    {
        Peer = peer;
        IsOnline = peer.IsOnline;
        Status = peer.Status;
        OnPropertyChanged(nameof(Pseudo));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(OnlineText));
    }

    public void AddMessage(MessageViewModel message)
    {
        Messages.Add(message);
        LastMessage = message.IsFile ? $"📎 {message.FileName}" : message.Content;
        LastTime = message.Time;
    }
}
