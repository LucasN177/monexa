# 🪙 MonExA: MONey + EXpenses + Analysis

**MonExA** ist eine hochperformante Progressive Web App (PWA), die darauf spezialisiert ist, komplexe Finanzstrukturen über mehrere Projekte und Benutzer hinweg zu zentralisieren. Entwickelt mit **C# Blazor WebAssembly**, bietet sie die Power einer Desktop-Anwendung direkt im Browser – optimiert für die mobile Nutzung, ohne die Hürden proprietärer App-Stores.

---

## 🚀 Vision & Problemstellung
Wer mehrere Projekte (Freelancing, Startups, private Investitionen) gleichzeitig jongliert, stößt bei herkömmlichen Banking-Apps oft an Grenzen. FinFlow löst dieses Problem durch:

* **Granulare Trennung:** Volle Kontrolle über verschiedene Projekte in einer zentralen Oberfläche.
* **Kollaboration:** Einladungsmanagement für Teammitglieder pro Projekt.
* **Plattformunabhängigkeit:** Dank Blazor WASM als PWA voll einsatzfähig auf iOS & Android – ohne App-Store-Zwang und zusätzliche Developer-Gebühren.

---

## ✨ Kernfunktionen

### 👥 Multi-User & Rollenmanagement
Das System basiert auf einer strikten Mandantentrennung. Benutzer können:
* Eigene Projekte erstellen und andere Nutzer per E-Mail dazu einladen.
* Zugriffsrechte granular steuern (Leserechte vs. Adminrechte).
* Echtzeit-Synchronisation bei Änderungen durch Teammitglieder (powered by Supabase Realtime).

### 🏦 Intelligente Kontenverwaltung
* Anlage beliebig vieler Unterkonten pro Projekt (z.B. Puffer, Steuern, Operating).
* **Transaction Tracking:** Präzise Erfassung von Umsätzen mit Kategorisierung und Metadaten.
* **Analytik:** Dynamische Visualisierung der Cashflow-Entwicklung und Budget-Verteilung.

---

## 🛠 Tech-Stack & Architektur
Dieses Projekt nutzt modernste Cloud-Technologien für maximale Skalierbarkeit und Performance:

| Komponente | Technologie | Zweck |
| :--- | :--- | :--- |
| **Frontend** | **Blazor WebAssembly** | C# im Browser via WebAssembly für eine native User Experience. |
| **Backend/DB** | **Supabase (PostgreSQL)** | Datenbank, Authentifizierung und Realtime-Engine. |
| **Security** | **Row Level Security (RLS)** | Absicherung der Daten direkt auf Datenbankebene. |
| **Styling** | **MudBlazor / Tailwind** | Modernes UI/UX Design mit reaktiven Komponenten. |

---

## 🏗 Roadmap: Die Zukunft von MonExA

Das Projekt befindet sich in aktiver Entwicklung. Geplante Meilensteine sind:

- [x] **Phase 1:** Core-Architektur, Authentifizierung & Manuelle Buchungen.
- [ ] **Phase 2:** Implementierung von **Open Banking APIs** (PSD2/FinTS), um echte Bankkonten direkt per C#-Backend anzubinden.
- [ ] **Phase 3:** KI-gestützte Kategorisierung von Umsätzen basierend auf historischen Daten Mustern.
- [ ] **Phase 4:** Export-Modul für Steuerberater (CSV, DATEV-Format).

---

## ⚙️ Installation & Local Setup

Die Möglichkeit das Projekt lokal zu hosten ist aktuell in Planung.

---

## 💡 Warum Blazor WASM & Supabase?

Die Entscheidung für diesen Stack war strategisch motiviert:

* **Code-Sharing:** Durch die Verwendung von C# auf dem Client bleibt die Business-Logik konsistent und typsicher.
* **Keine Serverkosten:** Durch das Hosting als statische Website (PWA) und der Nutzung von Supabase als Backend-as-a-Service bleibt die Infrastruktur extrem kosteneffizient.
* **Mobile First:** Die PWA-Struktur ermöglicht es, die App per "Home-Screen"-Shortcut zu nutzen. Dies umgeht die hohen Einstiegshürden des Apple Developer Programs, während die Performance durch WebAssembly nahezu nativ bleibt.

---

> **Hinweis:** Dies ist ein Open-Source-Projekt zu Demonstrationszwecken für mein Portfolio. Feedback und Pull Requests sind herzlich willkommen!

### Kontakt
Erstellt von **Lucas Nagelsmann** – [LinkedIn](https://www.linkedin.com/in/lucas-nagelsmann-1bb1b7340/) | [Portfolio](https://lucas-nagelsmann.de)
