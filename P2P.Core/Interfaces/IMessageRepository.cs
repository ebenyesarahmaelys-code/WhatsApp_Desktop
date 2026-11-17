using P2P.Core.Models;

namespace P2P.Core.Interfaces;

/// <summary>Historique des messages en SQLite (membre 4).</summary>
public interface IMessageRepository
{
    void Save(ChatMessage message);

    /// <summary>Tous les messages entre moi et ce pair, triés du plus ancien au plus récent.</summary>
    List<ChatMessage> GetConversation(string myId, string peerId);
}

/// <summary>Contacts déjà vus, en SQLite (membre 4).</summary>
public interface IContactRepository
{
    void SaveOrUpdate(Peer peer);

    /// <summary>Contacts récemment vus, du plus récent au plus ancien.</summary>
    List<Peer> GetRecent(int max = 10);
}
