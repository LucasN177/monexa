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
* Anlage beliebig vieler Unterkonten pro Projekt (z.B. Puffer, Steuern, Operating) mit Detailseite und Saldoverlauf.
* **Transaction Tracking:** Einnahmen, Ausgaben und Umbuchungen mit Kategorien, Notizen, Filtern (Zeitraum, Konto, Kategorie, Typ) und Mehrfachauswahl.
* **Automatische Salden:** Kontostände werden aus Anfangssaldo und Buchungen berechnet.
* **Kategorie-Vorschläge:** Neue Buchungen erhalten anhand früherer Buchungen automatisch eine passende Kategorie.

### 🔁 Daueraufträge, Budgets & Sparziele
* **Daueraufträge & Abos:** wöchentlich bis jährlich; fällige Termine werden automatisch verbucht, Fixkostenübersicht pro Monat.
* **Budgets:** pro Kategorie oder für alle Ausgaben, wöchentlich/monatlich/jährlich, mit Ampel, Hochrechnung und Monatsnavigation.
* **Sparziele:** Zielbetrag, Zieldatum, Ein-/Auszahlungen und benötigte Monatsrate.

### 📊 Analysen & Berichte
* Dashboard mit Vermögen, Cashflow-Chart, Ausgaben nach Kategorie und anstehenden Buchungen.
* Budget-Analyse mit kumuliertem Ausgabenverlauf, Insights und Monatsvergleich.
* Monats- und Jahresberichte sowie Zeitraumvergleiche (druckbar).
* **Export/Import:** CSV (Excel-kompatibel) und JSON-Komplettbackup pro Projekt.

---

## 🛠 Tech-Stack & Architektur
Dieses Projekt nutzt modernste Cloud-Technologien für maximale Skalierbarkeit und Performance:

| Komponente | Technologie | Zweck |
| :--- | :--- | :--- |
| **Frontend** | **Blazor WebAssembly** | C# im Browser via WebAssembly für eine native User Experience. |
| **Backend/DB** | **Supabase (PostgreSQL)** | Datenbank, Authentifizierung und Realtime-Engine. |
| **Security** | **Row Level Security (RLS)** | Absicherung der Daten direkt auf Datenbankebene. |
| **Styling** | **MudBlazor** | Modernes UI/UX Design mit reaktiven Komponenten. |
| **Offline/Demo** | **localStorage** | Voll funktionsfähiger Betrieb ohne Backend. |

---

## 🏗 Roadmap: Die Zukunft von MonExA

Das Projekt befindet sich in aktiver Entwicklung. Geplante Meilensteine sind:

- [x] **Phase 1:** Core-Architektur, Authentifizierung & Manuelle Buchungen.
- [ ] **Phase 2:** Implementierung von **Open Banking APIs** (PSD2/FinTS), um echte Bankkonten direkt per C#-Backend anzubinden.
- [ ] **Phase 3:** KI-gestützte Kategorisierung von Umsätzen basierend auf historischen Daten Mustern. *(Erste Stufe: regelbasierte Vorschläge aus früheren Buchungen ✔)*
- [ ] **Phase 4:** Export-Modul für Steuerberater (CSV ✔, DATEV-Format offen).

Details zum aktuellen Ausbau: [docs/IMPLEMENTIERUNGSPLAN.md](docs/IMPLEMENTIERUNGSPLAN.md)

---

## ⚙️ Installation & Local Setup

Voraussetzung: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --launch-profile http
```

Die App läuft dann unter `http://localhost:5227`.

### Demo-Modus (ohne Backend)
Ist in `wwwroot/appsettings.json` kein Supabase-Projekt eingetragen, startet MonExA im **Demo-Modus**:
Alle Daten liegen im `localStorage` des Browsers und werden beim ersten Start mit Beispieldaten befüllt.
Unter *Einstellungen* lassen sich die Daten jederzeit zurücksetzen.

### Cloud-Modus mit Supabase
1. Projekt auf [supabase.com](https://supabase.com) anlegen.
2. Datenbank einrichten (Details in [`database/README.md`](database/README.md)):
   * **Neues, leeres Projekt:** Inhalt von [`database/setup_all.sql`](database/setup_all.sql) im SQL-Editor ausführen
     (Tabellen, Row Level Security, Rollen, Einladungen, Realtime). Die Einzelskripte liegen in `database/setup/`.
   * **Bestehendes älteres MonExA-Schema:** stattdessen [`supabase/migrations/0001_init.sql`](supabase/migrations/0001_init.sql)
     ausführen – baut die alten Tabellen verlustfrei um (Spalten werden umbenannt, Daten übernommen, Kontostände bleiben erhalten).
3. Für die lokale Entwicklung URL und **anon/publishable Key** (Settings → API) in
   `wwwroot/appsettings.Development.json` eintragen (Datei ist per `.gitignore` ausgeschlossen):
   ```json
   { "Supabase": { "Url": "https://<projekt>.supabase.co", "AnonKey": "<publishable-key>" } }
   ```
   Niemals den `service_role`/secret Key verwenden – der Schutz erfolgt über RLS.
4. Für das GitHub-Pages-Deployment URL und Key als Repository-Secrets `SUPABASE_URL` und `SUPABASE_KEY` hinterlegen;
   der Workflow schreibt sie beim Build in `appsettings.json`. Ohne Secrets wird im Demo-Modus veröffentlicht.
5. Unter *Authentication → URL Configuration* die App-URLs als Redirect-URLs eintragen (für Bestätigungsmails und Magic Links).

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
