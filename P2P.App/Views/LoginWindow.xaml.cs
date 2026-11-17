using System.Windows;
using P2P.App.Helpers;
using P2P.App.ViewModels;

namespace P2P.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        IpBox.Text = IpHelper.GetLocalIp();
        PseudoBox.Text = Environment.UserName;
        PseudoBox.Focus();
        PseudoBox.SelectAll();
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        var pseudo = PseudoBox.Text.Trim();
        if (pseudo.Length < 2)
        {
            ErrorText.Text = "Le pseudo doit contenir au moins 2 caractères.";
            return;
        }
        if (!int.TryParse(PortBox.Text, out int port) || port < 1024 || port > 65535)
        {
            ErrorText.Text = "Port invalide (entre 1024 et 65535).";
            return;
        }

        try
        {
            var network = AppServices.CreateNetwork();
            var files = AppServices.CreateFileTransfer(network);
            var vm = new MainViewModel(network, files,
                AppServices.CreateMessageRepository(),
                AppServices.CreateContactRepository());

            // On démarre APRÈS avoir créé le ViewModel pour ne rater aucun événement
            network.Start(pseudo, port);

            new MainWindow(vm).Show();
            Close();
        }
        catch (Exception ex)
        {
            // Ex : port déjà utilisé par une autre instance
            ErrorText.Text = $"Impossible de démarrer : {ex.Message}";
        }
    }
}
