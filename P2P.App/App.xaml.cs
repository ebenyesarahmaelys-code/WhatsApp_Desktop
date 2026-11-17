using System.Windows;
using P2P.App.Views;

namespace P2P.App;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Affiche les erreurs au lieu de fermer l'appli sans rien dire
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        new LoginWindow().Show();
    }
}
