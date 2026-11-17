using System.Net;
using System.Windows;
using System.Windows.Controls;
using P2P.Core.Models;

namespace P2P.App.Views;

public partial class AddPeerWindow : Window
{
    public string PeerIp { get; private set; } = "";
    public int PeerPort { get; private set; }

    public AddPeerWindow(List<Peer> recentContacts)
    {
        InitializeComponent();
        RecentList.ItemsSource = recentContacts;
        if (recentContacts.Count == 0)
        {
            RecentLabel.Visibility = Visibility.Collapsed;
            RecentList.Visibility = Visibility.Collapsed;
        }
        IpBox.Focus();
    }

    private void OnRecentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (RecentList.SelectedItem is not Peer p) return;
        IpBox.Text = p.Ip;
        PortBox.Text = p.Port.ToString();
    }

    private void OnConnectClick(object sender, RoutedEventArgs e)
    {
        if (!IPAddress.TryParse(IpBox.Text.Trim(), out _))
        {
            ErrorText.Text = "Adresse IP invalide (ex : 192.168.1.20).";
            return;
        }
        if (!int.TryParse(PortBox.Text, out int port) || port < 1024 || port > 65535)
        {
            ErrorText.Text = "Port invalide (entre 1024 et 65535).";
            return;
        }

        PeerIp = IpBox.Text.Trim();
        PeerPort = port;
        DialogResult = true;
    }
}
