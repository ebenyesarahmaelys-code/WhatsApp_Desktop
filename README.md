# WhatsApp Desktop P2P (C# / WPF)

Messagerie **pair-à-pair** sur réseau local, inspirée de WhatsApp Desktop.
Aucun serveur central : chaque PC est à la fois **client et serveur**.

## Fonctionnalités

- Choix du pseudo et du port au démarrage, affichage de l'IP locale
- Découverte automatique des contacts sur le réseau (UDP broadcast)
- Statut en ligne / hors ligne des contacts
- Ajout manuel d'un contact par adresse IP
- Messages texte en temps réel (TCP direct) avec accusé de réception ✓✓
- Badge des messages non lus, recherche de contacts
- Envoi de fichiers avec barre de progression
- Historique des conversations et des contacts (SQLite)

## Architecture

```
WhatsAppP2P.sln
├── P2P.Core      Modèles (Peer, ChatMessage) + interfaces communes + constantes
├── P2P.Network   TCP (messages), découverte UDP, transfert de fichiers
├── P2P.Data      SQLite : historique des messages et des contacts
└── P2P.App       Interface WPF (MVVM)
```

| Élément | Technologie | Port |
|---|---|---|
| Découverte des pairs | UDP broadcast | 5001 |
| Messages et fichiers | TCP | 5000 (modifiable) |
| Format des messages | JSON (taille sur 4 octets + contenu) | — |
| Historique | SQLite (`whatsapp.db`) | — |

## Lancer le projet

Prérequis : Windows, Visual Studio 2022, .NET 8.

1. Ouvrir `WhatsAppP2P.sln`
2. Définir **P2P.App** comme projet de démarrage (clic droit → *Définir comme projet de démarrage*)
3. `F5`

Pour tester sur **un seul PC** : lancer deux fois l'appli (`P2P.App/bin/Debug/net8.0-windows/WhatsAppP2P.exe`)
avec deux ports différents (5000 et 5002).

Pour tester sur **deux PC** : même réseau Wi-Fi, et autoriser l'appli dans le pare-feu Windows au premier lancement.

## Équipe

| Membre | Partie | Branche |
|---|---|---|
| Bernard | Core, interface WPF, intégration, README | `develop` / `feature/ui` |
| Membre 2 | Réseau TCP (messages) | `feature/network-tcp` |
| Membre 3 | Découverte UDP + transfert de fichiers | `feature/discovery-files` |
| Membre 4 | Base de données SQLite | `feature/data` |

## Intégration

L'interface fonctionne d'abord avec des **services factices** (`P2P.App/Services/Fake*.cs`).
Quand une partie est terminée, on remplace la ligne correspondante dans `P2P.App/AppServices.cs`.
