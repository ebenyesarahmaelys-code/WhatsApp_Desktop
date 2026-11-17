using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using P2P.Core.Interfaces;
using P2P.Core.Models;

namespace P2P.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly INetworkService _network;
    private readonly IFileTransferService _files;
    private readonly IMessageRepository? _messages;
    private readonly IContactRepository? _contacts;

    private PeerViewModel? _selectedPeer;
    private string _messageText = "";
    private string _searchText = "";

    /// <summary>Déclenché quand un message est ajouté à la conversation affichée (pour scroller en bas).</summary>
    public event Action? MessageAdded;

    public MainViewModel(INetworkService network, IFileTransferService files,
                         IMessageRepository? messages, IContactRepository? contacts)
    {
        _network = network;
        _files = files;
        _messages = messages;
        _contacts = contacts;

        PeersView = CollectionViewSource.GetDefaultView(Peers);
        PeersView.Filter = FilterPeer;

        SendCommand = new RelayCommand(async () => await SendAsync(), () => SelectedPeer != null && !string.IsNullOrWhiteSpace(MessageText));
        SendFileCommand = new RelayCommand(async () => await SendFileAsync(), () => SelectedPeer != null);

        // Événements réseau (arrivent sur un thread réseau -> on repasse sur le thread UI)
        _network.PeerDiscovered += p => OnUi(() => OnPeerDiscovered(p));
        _network.PeerLost += p => OnUi(() => OnPeerLost(p));
        _network.MessageReceived += m => OnUi(() => OnMessageReceived(m));
        _files.ProgressChanged += (name, pct) => OnUi(() => OnFileProgress(name, pct));
        _files.FileReceived += (fromId, path) => OnUi(() => OnFileReceived(fromId, path));

        // Contacts déjà connus (base de données), affichés hors ligne en attendant la découverte
        if (_contacts != null)
            foreach (var c in _contacts.GetRecent(50))
            {
                c.IsOnline = false;
                Peers.Add(new PeerViewModel(c));
            }
    }

    // ================== Propriétés liées à l'interface ==================

    public Peer Me => _network.LocalPeer;
    public string MyInitials => string.IsNullOrEmpty(Me.Pseudo) ? "?" : Me.Pseudo[..Math.Min(2, Me.Pseudo.Length)].ToUpper();
    public string MyInfo => $"En ligne · {Me.Ip}:{Me.Port}";
    public string WindowTitle => $"WhatsApp P2P — {Me.Pseudo} · {Me.Ip}:{Me.Port}";

    public ObservableCollection<PeerViewModel> Peers { get; } = new();
    public ICollectionView PeersView { get; }

    public PeerViewModel? SelectedPeer
    {
        get => _selectedPeer;
        set
        {
            if (!SetProperty(ref _selectedPeer, value)) return;
            OnPropertyChanged(nameof(HasSelectedPeer));
            if (value != null)
            {
                value.UnreadCount = 0;
                LoadHistory(value);
                MessageAdded?.Invoke();
            }
        }
    }

    public bool HasSelectedPeer => SelectedPeer != null;

    public string MessageText { get => _messageText; set => SetProperty(ref _messageText, value); }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) PeersView.Refresh(); }
    }

    public ICommand SendCommand { get; }
    public ICommand SendFileCommand { get; }

    // ================== Actions de l'utilisateur ==================

    private async Task SendAsync()
    {
        var peer = SelectedPeer;
        var text = MessageText.Trim();
        if (peer == null || text.Length == 0) return;

        var msg = new ChatMessage
        {
            Type = MessageType.Text,
            FromId = Me.Id,
            FromPseudo = Me.Pseudo,
            FromPort = Me.Port,
            ToId = peer.Id,
            Content = text
        };

        var vm = new MessageViewModel(msg, isMine: true);
        peer.AddMessage(vm);
        MessageText = "";
        MessageAdded?.Invoke();
        _messages?.Save(msg);

        try
        {
            await _network.SendMessageAsync(peer.Peer, msg);
            if (vm.State == DeliveryState.Sending) vm.State = DeliveryState.Sent;
        }
        catch (Exception ex)
        {
            vm.State = DeliveryState.Failed;
            MessageBox.Show($"Message non envoyé : {ex.Message}", "Erreur d'envoi",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task SendFileAsync()
    {
        var peer = SelectedPeer;
        if (peer == null) return;

        var dialog = new OpenFileDialog { Title = $"Envoyer un fichier à {peer.Pseudo}" };
        if (dialog.ShowDialog() != true) return;

        var info = new System.IO.FileInfo(dialog.FileName);
        var msg = new ChatMessage
        {
            Type = MessageType.FileStart,
            FromId = Me.Id,
            FromPseudo = Me.Pseudo,
            ToId = peer.Id,
            FileName = info.Name,
            FileSize = info.Length,
            Content = ""
        };

        var vm = new MessageViewModel(msg, isMine: true)
        {
            LocalPath = info.FullName,
            FileInfo = $"{FormatSize(info.Length)} · envoi en cours"
        };
        peer.AddMessage(vm);
        MessageAdded?.Invoke();

        try
        {
            await _files.SendFileAsync(peer.Peer, info.FullName);
            vm.Progress = 100;
            vm.FileInfo = $"{FormatSize(info.Length)} · envoyé";
            vm.State = DeliveryState.Delivered;
            _messages?.Save(msg);
        }
        catch (Exception ex)
        {
            vm.State = DeliveryState.Failed;
            vm.FileInfo = "Échec de l'envoi";
            MessageBox.Show($"Fichier non envoyé : {ex.Message}", "Erreur d'envoi",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Appelé par la fenêtre "Ajouter un pair".</summary>
    public async Task<bool> AddManualPeerAsync(string ip, int port)
    {
        var peer = await _network.ConnectManuallyAsync(ip, port);
        if (peer == null) return false;
        OnPeerDiscovered(peer);
        SelectedPeer = Peers.FirstOrDefault(p => p.Id == peer.Id);
        return true;
    }

    public List<Peer> GetRecentContacts() => _contacts?.GetRecent() ?? new List<Peer>();

    public void Shutdown() => _network.Stop();

    // ================== Événements réseau ==================

    private void OnPeerDiscovered(Peer peer)
    {
        if (peer.Id == Me.Id) return; // c'est moi

        peer.IsOnline = true;
        peer.LastSeen = DateTime.Now;
        var existing = Peers.FirstOrDefault(p => p.Id == peer.Id);
        if (existing == null) Peers.Add(new PeerViewModel(peer));
        else existing.Update(peer);

        _contacts?.SaveOrUpdate(peer);
    }

    private void OnPeerLost(Peer peer)
    {
        var existing = Peers.FirstOrDefault(p => p.Id == peer.Id);
        if (existing != null) existing.IsOnline = false;
    }

    private void OnMessageReceived(ChatMessage msg)
    {
        switch (msg.Type)
        {
            case MessageType.Ack:
                // Content = Id du message que l'autre a bien reçu
                var sent = Peers.SelectMany(p => p.Messages).FirstOrDefault(m => m.Id == msg.Content);
                if (sent != null) sent.State = DeliveryState.Delivered;
                break;

            case MessageType.Text:
                var peer = FindOrCreatePeer(msg);
                peer.AddMessage(new MessageViewModel(msg, isMine: false));
                _messages?.Save(msg);
                if (peer == SelectedPeer) MessageAdded?.Invoke();
                else peer.UnreadCount++;
                break;

            case MessageType.FileStart:
            case MessageType.FileChunk:
            case MessageType.FileEnd:
                _files.HandleIncoming(msg);
                break;

            case MessageType.Profile:
                // TODO (paramètres) : afficher la photo de profil reçue (msg.Content en Base64)
                break;
        }
    }

    private void OnFileProgress(string fileName, double pct)
    {
        var vm = SelectedPeer?.Messages.LastOrDefault(m => m.IsFile && m.FileName == fileName && m.IsMine)
                 ?? Peers.SelectMany(p => p.Messages).LastOrDefault(m => m.IsFile && m.FileName == fileName && m.IsMine);
        if (vm == null) return;
        vm.Progress = pct;
        vm.FileInfo = $"{FormatSize(vm.Message.FileSize)} · {pct:0} %";
    }

    private void OnFileReceived(string fromId, string path)
    {
        var peer = Peers.FirstOrDefault(p => p.Id == fromId);
        if (peer == null) return;

        var info = new System.IO.FileInfo(path);
        var msg = new ChatMessage
        {
            Type = MessageType.FileEnd,
            FromId = fromId,
            ToId = Me.Id,
            FileName = info.Name,
            FileSize = info.Exists ? info.Length : 0
        };
        peer.AddMessage(new MessageViewModel(msg, isMine: false)
        {
            Progress = 100,
            LocalPath = path,
            FileInfo = $"{FormatSize(msg.FileSize)} · reçu (double-clic pour ouvrir)"
        });
        _messages?.Save(msg);
        if (peer == SelectedPeer) MessageAdded?.Invoke();
        else peer.UnreadCount++;
    }

    // ================== Utilitaires ==================

    private PeerViewModel FindOrCreatePeer(ChatMessage msg)
    {
        var peer = Peers.FirstOrDefault(p => p.Id == msg.FromId);
        if (peer != null) return peer;

        // Message d'un pair pas encore découvert : on l'ajoute
        peer = new PeerViewModel(new Peer
        {
            Id = msg.FromId,
            Pseudo = string.IsNullOrEmpty(msg.FromPseudo) ? "Inconnu" : msg.FromPseudo,
            Port = msg.FromPort,
            IsOnline = true
        });
        Peers.Add(peer);
        return peer;
    }

    private void LoadHistory(PeerViewModel peer)
    {
        if (_messages == null || peer.Messages.Count > 0) return;
        foreach (var m in _messages.GetConversation(Me.Id, peer.Id))
            peer.AddMessage(new MessageViewModel(m, isMine: m.FromId == Me.Id) { State = DeliveryState.Delivered, Progress = 100 });
    }

    private bool FilterPeer(object item)
        => string.IsNullOrWhiteSpace(SearchText)
           || ((PeerViewModel)item).Pseudo.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} o",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} Ko",
        _ => $"{bytes / (1024.0 * 1024):0.#} Mo"
    };
}
