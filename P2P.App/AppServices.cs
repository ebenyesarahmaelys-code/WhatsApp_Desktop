using P2P.App.Services;
using P2P.Core.Interfaces;

namespace P2P.App;

/// <summary>
/// UN SEUL endroit où l'on choisit les implémentations.
/// Pour l'instant : faux services (pour développer l'interface sans attendre le réseau).
/// Quand les autres ont fini, remplacer chaque ligne par la vraie classe.
/// </summary>
public static class AppServices
{
    public static INetworkService CreateNetwork()
        => new FakeNetworkService();
        // TODO intégration : => new P2P.Network.NetworkService();

    public static IFileTransferService CreateFileTransfer(INetworkService network)
        => new FakeFileTransferService();
        // TODO intégration : => new P2P.Network.Files.FileTransferService(network);

    public static IMessageRepository? CreateMessageRepository()
        => null;
        // TODO intégration : => new P2P.Data.Repositories.MessageRepository();

    public static IContactRepository? CreateContactRepository()
        => null;
        // TODO intégration : => new P2P.Data.Repositories.ContactRepository();
}
