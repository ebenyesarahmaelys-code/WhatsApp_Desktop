using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using P2P.App.ViewModels;

namespace P2P.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Descendre automatiquement en bas de la conversation
        vm.MessageAdded += () => Dispatcher.BeginInvoke(() => MessagesScroll.ScrollToEnd());

        Closed += (_, _) => vm.Shutdown();
    }

    private async void OnAddPeerClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddPeerWindow(_vm.GetRecentContacts()) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        bool ok = await _vm.AddManualPeerAsync(dialog.PeerIp, dialog.PeerPort);
        if (!ok)
            MessageBox.Show($"Impossible de joindre {dialog.PeerIp}:{dialog.PeerPort}.\nVérifie que l'appli est lancée sur l'autre PC.",
                "Pair injoignable", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        // TODO : fenêtre Paramètres (photo de profil, statut, dossier de réception, thème)
        MessageBox.Show("Paramètres : bientôt disponible.", "Paramètres");
    }

    private void OnCallClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Les appels audio et vidéo sont prévus dans une prochaine version.", "Fonctionnalité à venir",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Double-clic sur un fichier : l'ouvrir.</summary>
    private void OnMessageClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if ((sender as FrameworkElement)?.DataContext is not MessageViewModel { IsFile: true, LocalPath: { } path }) return;
        if (!System.IO.File.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
