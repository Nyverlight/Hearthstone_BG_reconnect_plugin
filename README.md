# BG Reconnect (mod Firestone / BepInEx)

Reconnexion à une partie Hearthstone **Battlegrounds** sur Windows, avec un raccourci clavier ou un bouton flottant. C'est l'équivalent Windows de [HS Reconnect pour macOS](https://github.com/kulibabkaaa/Hearthstone-Reconnect-MacOS), mais sous forme de mod BepInEx géré par Firestone.

**Téléchargement :** la DLL se trouve dans les [releases](https://github.com/Nyverlight/Hearthstone_BG_reconnect_plugin/releases).

> ⚠️ Ce mod est chargé dans le processus du jeu. Modifier le client va contre les conditions d'utilisation de Blizzard : utilisation à tes risques.

## Ce que ça fait

- **Ctrl+Shift+W** pendant une partie Battlegrounds coupe la connexion au serveur de partie, puis Hearthstone se reconnecte tout seul.
- Le **bouton « Reconnect »** apparaît en haut à droite pendant une partie Battlegrounds et fait la même chose. **Clic droit + glisser** le déplace, et la position est enregistrée.
- Un délai de 5 s s'applique entre deux reconnexions.
- Le mod ne fait rien hors Battlegrounds, en spectateur ou une fois la partie terminée.

### Comment ça marche

L'app macOS doit passer par un proxy réseau pour couper la connexion. Ici, le mod tourne dans le jeu et appelle directement `Network.SimulateUncleanDisconnectFromGameServer()`. Cette méthode ferme le socket du serveur de partie comme une vraie coupure réseau, et le `ReconnectMgr` du jeu rejoint la même partie. Aucun droit administrateur n'est nécessaire et aucun patch Harmony n'est appliqué.

Si un patch de Hearthstone renomme ces méthodes, le mod se désactive proprement. Il écrit alors une erreur dans `BepInEx\LogOutput.log` au lieu de faire planter le jeu. Il suffit de le recompiler contre la nouvelle version.

## Installation

Pré-requis : les mods activés dans **Firestone › Paramètres › Mods**, ce qui installe BepInEx.

1. Hearthstone fermé, copier `com.community.hs.BgReconnect.dll` dans `<Hearthstone>\BepInEx\plugins\`.
2. Lancer Hearthstone. Au premier lancement, le mod crée `BepInEx\config\com.community.hs.BgReconnect.cfg`, puis apparaît dans Firestone › Mods, où on peut l'activer ou le désactiver.

Désinstallation : supprimer la DLL et le `.cfg`, ou désactiver le mod dans Firestone.

## Configuration

Les réglages se trouvent dans `BepInEx\config\com.community.hs.BgReconnect.cfg` :

| Clé | Défaut | Rôle |
| --- | --- | --- |
| `Reconnect.Hotkey` | `W + LeftControl + LeftShift` | Raccourci de reconnexion |
| `Reconnect.CooldownSeconds` | `5` | Délai minimal entre deux reconnexions |
| `Button.ShowButton` | `true` | Afficher le bouton flottant |
| `Button.ButtonX` / `ButtonY` | `0.86` / `0.02` | Position du bouton (0–1) |

## Compiler

Il faut le SDK .NET (8 ou plus) et Hearthstone installé avec BepInEx.

```bash
dotnet build -c Release
```

Pour une autre installation de Hearthstone : `-p:HearthstoneDir="D:\Games\Hearthstone"`. Pour copier automatiquement la DLL dans `BepInEx\plugins` : `-p:DeployToGame=true` (avec Hearthstone fermé).

La DLL compilée se trouve dans `bin\Release\com.community.hs.BgReconnect.dll`. Le projet compile contre les DLL locales du jeu, de Unity et de BepInEx, et n'en redistribue aucune.
