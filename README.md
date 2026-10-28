# 🪙 MonExA: MONey + EXpenses + Analysis

**MonExA** ist eine Progressive Web App (PWA), die Finanzen über mehrere Projekte und Benutzer hinweg zentral verwaltet. Entwickelt mit **C# Blazor WebAssembly** und **Supabase**, bietet sie die Funktionen einer Desktop-Anwendung direkt im Browser – optimiert für die mobile Nutzung, ohne die Hürden proprietärer App-Stores.

---

## 🚀 Vision & Problemstellung
Wer mehrere Projekte (Freelancing, Startups, private Investitionen) gleichzeitig jongliert, stößt bei herkömmlichen Banking-Apps schnell an Grenzen. MonExA löst dieses Problem durch:

* **Granulare Trennung:** Jedes Projekt hat eigene Konten, Kategorien, Budgets und Auswertungen – alles in einer Oberfläche.
* **Kollaboration:** Teammitglieder werden pro Projekt per E-Mail eingeladen und erhalten passende Rechte.
* **Plattformunabhängigkeit:** Als PWA auf iOS, Android und Desktop nutzbar – ohne App-Store-Zwang und Developer-Gebühren.

---

## ✨ Kernfunktionen

### 👥 Projekte, Benutzer & Rollen
* Eigene Projekte anlegen, umbenennen und löschen; Wechsel per Projektmenü in der Kopfzeile.
* Mitglieder per E-Mail einladen – die Einladung wird bei der nächsten Anmeldung automatisch angenommen.
* Drei Rollen pro Projekt: **Leser** (nur ansehen), **Bearbeiter** (Buchungen, Konten & Co. ändern), **Admin** (zusätzlich Mitglieder und Projekt verwalten).
* Anmeldung per E-Mail/Passwort oder Magic Link, Passwort-Reset.
* Echtzeit-Synchronisation: Änderungen anderer Mitglieder erscheinen ohne Neuladen (Supabase Realtime).

### 🏦 Konten & Buchungen
* Beliebig viele Konten pro Projekt (Girokonto, Sparkonto, Tagesgeld, Kreditkarte, Depot) mit Detailseite und Saldoverlauf.
* Einnahmen, Ausgaben und Umbuchungen mit Kategorien und Notizen; Filter nach Zeitraum, Konto, Kategorie und Typ, Mehrfachauswahl.
* **Automatische Salden:** Kontostände werden aus Anfangssaldo und Buchungen berechnet.
* **Kategorie-Vorschläge:** Neue Buchungen erhalten anhand früherer Buchungen mit ähnlicher Beschreibung eine passende Kategorie.

### 🔁 Daueraufträge, Budgets & Sparziele
* **Daueraufträge & Abos:** wöchentlich bis jährlich; fällige Termine werden automatisch verbucht, inkl. Fixkostenübersicht pro Monat.
* **Budgets:** pro Kategorie oder für alle Ausgaben, wöchentlich/monatlich/jährlich, mit Ampel, Hochrechnung bis Periodenende und Monatsnavigation.
* **Sparziele:** Zielbetrag, Zieldatum, Ein- und Auszahlungen sowie die benötigte Sparrate pro Monat.

### 📊 Analysen & Berichte
* Dashboard mit Vermögen, Cashflow- und Vermögensverlauf, Ausgaben nach Kategorie, Budgets und anstehenden Buchungen.
* Budget-Analyse mit kumuliertem Ausgabenverlauf, Insights und Vergleich zu Vormonat und 3-Monats-Schnitt.
* Monats- und Jahresberichte sowie frei wählbare Zeitraumvergleiche – druckbar.
* **Export/Import:** Buchungen als CSV (Excel-kompatibel) und JSON-Komplettbackup pro Projekt.

---

## 🛠 Tech-Stack & Architektur

| Komponente | Technologie | Zweck |
| :--- | :--- | :--- |
| **Frontend** | **Blazor WebAssembly (.NET 9)** | C# im Browser via WebAssembly. |
| **UI** | **MudBlazor** | Material-Design-Komponenten, Charts, Dark Mode. |
| **Backend/DB** | **Supabase (PostgreSQL)** | Datenbank, Authentifizierung und Realtime – angebunden über das offizielle `Supabase`-NuGet-Paket. |
| **Security** | **Row Level Security (RLS)** | Rechteprüfung direkt in der Datenbank, pro Projekt und Rolle. |
| **Offline/Demo** | **localStorage** | Voll funktionsfähiger Betrieb ganz ohne Backend. |
| **Hosting** | **GitHub Pages** | Statisches Hosting, Deployment per GitHub Actions. |

```
Seiten & Layout  ──►  FinanceState  (zentraler Zustand, Fachlogik, Change-Events)
                           │
                           ▼
                      IDataStore
                 ┌─────────┴──────────┐
       SupabaseDataStore       LocalDataStore
       (PostgREST + RLS)       (localStorage, Demo)
                 ▲
           AuthService  (Supabase Auth, Session im localStorage)
```

**Betriebsmodi:** Ist ein Supabase-Projekt konfiguriert, läuft die App im **Cloud-Modus** mit Login. Ohne Konfiguration startet sie im **Demo-Modus** und legt beim ersten Start Beispieldaten im Browser an.

### Projektstruktur

```
├── Layout/                 Hauptlayout, Login-Layout, Theme
├── Models/                 Domänenmodelle (auf Postgres-Tabellen gemappt) und Enums
├── Pages/                  Seiten (Dashboard, Konten, Transaktionen, Budgets, Berichte, …)
│   └── KontoDialogs/       Formulare für Konten, Transaktionen und Kategorien
├── Services/               FinanceState, AuthService, Export, Realtime, Hilfsfunktionen
│   └── Data/               IDataStore mit Supabase- und localStorage-Implementierung
├── Shared/                 Wiederverwendbare Komponenten (Kennzahl-Kachel, Seitenkopf, …)
├── database/               SQL-Skripte zum kompletten Neuaufsetzen der Datenbank
├── supabase/migrations/    Upgrade-Skript für bestehende Datenbanken
├── docs/                   Implementierungsplan
└── wwwroot/                Statische Dateien, appsettings.json, PWA-Manifest
```

---

## ⚙️ Installation & Local Setup

Voraussetzung: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --launch-profile http
```

Die App läuft dann unter `http://localhost:5227`.

### Demo-Modus (ohne Backend)
Ohne Supabase-Konfiguration startet MonExA im **Demo-Modus**: Alle Daten liegen im `localStorage` des Browsers und werden beim ersten Start mit Beispieldaten befüllt (zwei Projekte, mehrere Monate Buchungen). Unter *Einstellungen* lassen sich die Daten jederzeit zurücksetzen.

### Cloud-Modus mit Supabase
1. Projekt auf [supabase.com](https://supabase.com) anlegen.
2. Datenbank einrichten (siehe [Datenbank](#-datenbank)).
3. URL und **Publishable/anon Key** (*Project Settings → API*) in `wwwroot/appsettings.Development.json` eintragen.
   Die Datei wird nur beim lokalen Start geladen und ist per `.gitignore` ausgeschlossen:
   ```json
   { "Supabase": { "Url": "https://<projekt>.supabase.co", "AnonKey": "<publishable-key>" } }
   ```
   Niemals den `service_role`- bzw. Secret-Key verwenden – der Schutz der Daten erfolgt über RLS.
4. Unter *Authentication → URL Configuration* `http://localhost:5227` und die veröffentlichte App-URL als Redirect-URLs eintragen (für Bestätigungsmails und Magic Links).

---

## 🗄 Datenbank

| Tabelle | Inhalt |
| :--- | :--- |
| `projects` | Projekte (Mandanten) mit Eigentümer |
| `project_members` | Mitglieder, Rollen und offene Einladungen |
| `accounts` | Konten mit Anfangssaldo |
| `categories` | Einnahme- und Ausgabekategorien |
| `transactions` | Buchungen (Einnahme, Ausgabe, Umbuchung) |
| `recurring_transactions` | Daueraufträge und Abos |
| `budgets` | Ausgabenlimits je Kategorie und Zeitraum |
| `savings_goals` | Sparziele |

Dazu kommen die Funktionen `project_role()` (Basis aller RLS-Policies), `accept_pending_invitations()` und ein Trigger, der den Eigentümer eines neuen Projekts als Admin einträgt.

* **Neue, leere Datenbank:** Inhalt von [`database/setup_all.sql`](database/setup_all.sql) im SQL-Editor ausführen. Die Einzelskripte pro Tabelle liegen in [`database/setup/`](database/setup/) – Details und Hinweise zu Änderungen in [`database/README.md`](database/README.md).
* **Bestehendes älteres MonExA-Schema:** stattdessen [`supabase/migrations/0001_init.sql`](supabase/migrations/0001_init.sql) ausführen. Es baut die alten Tabellen verlustfrei um (Spalten werden umbenannt, Daten übernommen, Kontostände bleiben erhalten).

Beide Skripte sind idempotent, laufen als eine Transaktion und führen zum gleichen Endzustand.

---

## 🚢 Deployment

Jeder Push auf `main` veröffentlicht die App per GitHub Actions auf GitHub Pages ([`.github/workflows/deploy.yml`](.github/workflows/deploy.yml)).

Für den Cloud-Modus im Deployment unter *Settings → Secrets and variables → Actions* zwei **Repository-Secrets** anlegen:

| Secret | Wert |
| :--- | :--- |
| `SUPABASE_URL` | `https://<projekt>.supabase.co` |
| `SUPABASE_KEY` | Publishable/anon Key |

Der Workflow schreibt sie beim Build in `wwwroot/appsettings.json`; ohne Secrets wird im Demo-Modus veröffentlicht. Der Key ist für den Browser bestimmt und steht danach in der veröffentlichten App – das ist bei einem Publishable Key so vorgesehen.

---

## 🏗 Roadmap: Die Zukunft von MonExA

- [x] **Phase 1:** Core-Architektur, Authentifizierung, Projekte & Rollen, manuelle Buchungen, Budgets, Daueraufträge, Sparziele, Berichte.
- [ ] **Phase 2:** Implementierung von **Open Banking APIs** (PSD2/FinTS), um echte Bankkonten direkt per C#-Backend anzubinden.
- [ ] **Phase 3:** KI-gestützte Kategorisierung von Umsätzen basierend auf historischen Daten. *(Erste Stufe: regelbasierte Vorschläge aus früheren Buchungen ✔)*
- [ ] **Phase 4:** Export-Modul für Steuerberater (CSV ✔, DATEV-Format offen).

Details zum aktuellen Ausbau: [docs/IMPLEMENTIERUNGSPLAN.md](docs/IMPLEMENTIERUNGSPLAN.md)

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
