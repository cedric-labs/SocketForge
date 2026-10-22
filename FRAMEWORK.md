# SocketForge

Framework partagé .NET d'observation, d'énumération et d'analyse des flux et sockets réseau (TCP/UDP) actifs au niveau système (Windows & Linux), **sans installation de pilote tiers ni dépendance Npcap/WinPcap**.

## 🚀 Capacités Clés

* **Zéro Npcap** :
  * Windows : inspection via l'API Win32 IP Helper (`iphlpapi.dll` : `GetExtendedTcpTable`, `GetExtendedUdpTable`).
  * Linux : inspection via le pseudo-système `/proc/net/tcp`, `/proc/net/udp` et corrélation des inodes de sockets dans `/proc/<pid>/fd`.
* **Corrélation Processus** :
  * Liaison directe de chaque socket active au processus propriétaire résolu via **`ProcessForge`**.
* **Typage fort & Clé d'unicité logique** :
  * Modélisation immuable (`ActiveSocketConnection`, `SocketEndpoint`, `SocketTransportProtocol`, `SocketFlowDirection`).
  * Clé de dédoublonnage canonique préservant l'identité applicative des flux sortants indépendamment des ports locaux éphémères.
