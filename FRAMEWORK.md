<p align="right">
  <b>English</b> | <a href="FRAMEWORK.fr.md">Français</a>
</p>

# SocketForge — Framework Specification & AI Documentation

This document is the technical manifest intended for AI agents and software engineers. It outlines all capabilities, architectural components, and public contracts exposed by **SocketForge**.

---

## 🎯 Scope & Responsibility

SocketForge is a cross-platform .NET system engineering library dedicated to **observing, enumerating, and analyzing active TCP and UDP sockets and network flows at the OS level (Windows & Linux)**, without external drivers or Npcap/WinPcap dependencies.

---

## 🚀 Key Capabilities

* **Zero Npcap Dependency**:
  * Windows: Native inspection via Win32 IP Helper API (`iphlpapi.dll`: `GetExtendedTcpTable`, `GetExtendedUdpTable`).
  * Linux: Direct kernel inspection via `/proc/net/tcp`, `/proc/net/udp` and inode correlation across `/proc/<pid>/fd`.
* **Flow Direction Resolution (`SocketFlowDirectionResolver`)**:
  * Accurate and deterministic classification of `Listening`, `Inbound`, and `Outbound` states across both Windows and Linux.
  * Local listener indexing (`LISTEN` / `0A` / State 2) to formally identify inbound connections destined for hosted services (SSH, HTTP, databases, etc.).
  * Heuristic fallback rules based on dynamic/ephemeral port allocations (Linux `>= 32768`, IANA `>= 49152`) and privileged ranges (`< 1024`).
* **Process Correlation**:
  * Direct correlation of every active socket to its owning process and underlying target executable resolved via **`ProcessForge`**.
* **Strong Typing & Canonical Deduplication Keys**:
  * Immutable data models (`ActiveSocketConnection`, `SocketEndpoint`, `SocketTransportProtocol`, `SocketFlowDirection`).
  * Canonical deduplication keys preserving logical application flows independently of client ephemeral port variations.

---

## 📋 Maintenance Protocol for AI Agents

All enhancements to SocketForge must comply with:
1. **Backward Compatibility:** Preserve all existing public contracts. Use overloads or optional parameters.
2. **Code Cleanliness:** Zero warnings (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zero pragmas/suppressions.
3. **Testing Standards:** Test naming convention `TargetMethod_ScenarioOrCondition_ExpectedResult`.
4. **Documentation:** Update both `FRAMEWORK.md` and `FRAMEWORK.fr.md` immediately upon introducing or modifying capabilities.
5. **Validation:** Full pass of `./verify.ps1`.
