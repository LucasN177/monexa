# Implementierungsplan – MonExA Feature-Ausbau

Branch: `feature/finance-core-and-supabase`

## 1. Ausgangslage (Stand `main`, Commit `6edf958`)

| Bereich | Zustand |
|---|---|
| Dashboard | Rein statische Zahlen und Charts, keine Datenanbindung |
| Konten | Liste mit In-Memory-Testdaten; Anlegen/Bearbeiten-Seiten speichern nichts (`Console.WriteLine`), Details-Button ohne Funktion, Löschen ohne Bestätigung, totes Inline-Dialog-Markup |
| Transaktionen | Eigenes lokales Modell, Testdaten; Anlegen speichert nichts; Route `/transaktionen/{id}/bearbeiten` fehlt (404); Radio-Group falsch typisiert (`T="bool"`) |
| Kategorien | Eigenes Modell (nicht mit Transaktionen verknüpft), Speichern nur simuliert, Statistiken hart codiert |
| Budgets | Eigenes Modell, `Spent` hart codiert, Zeitraum-Filter ohne Wirkung, Farbauswahl doppelt „Primary" |
| Analyse | Komplett statische Mockdaten, Monatsnavigation ändert nichts |
| Layout | Projektwahl, Profil, Einstellungen, Abmelden, Berichte ohne Funktion; Provider doppelt eingebunden; Dark-Mode nicht persistiert |
| Supabase | Nur im README erwähnt – **keine Integration im Code** |

Jede Seite hatte eigene Modelle und Testdaten – Änderungen auf einer Seite waren auf keiner anderen sichtbar.

## 2. Zielarchitektur

```
Pages / Layout  ──►  FinanceState (zentraler Zustand + Fachlogik, Change-Events)
                          │
                          ▼
                     IDataStore
                 ┌────────┴─────────┐
       SupabaseDataStore     LocalDataStore
       (PostgREST + RLS)     (localStorage, Demo/Offline)
                 ▲
           AuthService (Supabase GoTrue, Session in localStorage)
```

* **Ein gemeinsames Domänenmodell** (`Models/`): `Project`, `ProjectMember`, `Konto`, `Category`, `Transaction`, `Budget`, `RecurringTransaction`, `SavingsGoal`. Alle erben von `Supabase.Postgrest.Models.BaseModel` und sind per `[Table]`/`[Column]` auf Postgres gemappt.
* **Betriebsmodi**
  * *Cloud*: `wwwroot/appsettings.json` enthält `Supabase:Url` + `Supabase:AnonKey` → Login-Pflicht, Daten in Supabase, abgesichert per Row Level Security.
  * *Demo/Offline*: keine Konfiguration → Daten im `localStorage`, beim ersten Start mit Beispieldaten befüllt. Die App ist so ohne Backend lauffähig (z. B. GitHub Pages).
* **Kontostände** werden nicht mehr manuell gepflegt, sondern berechnet: `Anfangssaldo + Summe aller Buchungen`. Beim Bearbeiten des Kontostands wird der Anfangssaldo angepasst. Damit bleiben Konten auch bei mehreren Nutzern konsistent.
* **Datenbankschema**: `supabase/migrations/0001_init.sql` (Tabellen, Indizes, RLS-Policies, Rollenmodell, Einladungen).

## 3. Arbeitspakete

### Phase A – Fundament
1. Supabase-Paket, Konfiguration (`appsettings.json`), `SupabaseSettings`.
2. Domänenmodelle + Enums (`CategoryType`, `BudgetPeriod`, `RecurrenceInterval`, `ProjectRole`).
3. `IDataStore` mit `SupabaseDataStore` und `LocalDataStore` (inkl. Seed-Daten).
4. `AuthService` (Anmelden, Registrieren, Magic-Link, Abmelden, Session-Persistenz).
5. `FinanceState`: Laden pro Projekt, CRUD, berechnete Salden, Budget-Auswertung, Change-Event.
6. `UiHelpers`: einheitliche Texte/Icons/Farben (vorher in jeder Seite dupliziert).
7. SQL-Migration mit RLS.

### Phase B – Bestehende Features vervollständigen
1. **Layout**: funktionierende Projektwahl, „Neues Projekt", Profil/Einstellungen/Abmelden, Berichte-Links, Dark-Mode-Persistenz, Login-Weiterleitung, Ladezustand.
2. **Konten**: echtes CRUD, Lösch-Bestätigung, neue Detailseite `/konten/{id}` (Saldoverlauf + Buchungen).
3. **Transaktionen**: Anlegen *und* Bearbeiten (fehlende Route), Kategorien aus der Kategorieverwaltung, Zeitraumfilter, Lösch-Bestätigung.
4. **Kategorien**: persistiert, echte Statistiken (Anzahl, Summe, Ø, zuletzt verwendet), Löschen entkoppelt Transaktionen.
5. **Budgets**: an Kategorien gekoppelt, Ausgaben aus echten Buchungen je Zeitraum (Woche/Monat/Jahr), funktionierender Zeitraumfilter.
6. **Analyse**: berechnet aus echten Daten, Monatsnavigation wirkt, echter Vormonats-/3-Monats-Vergleich.
7. **Dashboard**: echte KPIs mit Vormonatsvergleich, Cashflow-Chart (6 Monate), Ausgaben-Donut, letzte Buchungen.
8. MudBlazor-8-API-Korrekturen (`@bind-Value` statt `@bind-Checked`, `@bind-Visible`, typisierte Radio-Group).

### Phase C – Neue Features
1. **Daueraufträge / Abos** (`/dauerauftraege`): wiederkehrende Buchungen (wöchentlich … jährlich), automatische Verbuchung fälliger Termine beim Laden, „Jetzt buchen", monatliche Fixkostenübersicht.
2. **Sparziele** (`/sparziele`): Zielbetrag, Zieldatum, Einzahlungen, Fortschritt und benötigte Monatsrate.
3. **Berichte** (`/berichte/monat`, `/berichte/jahr`, `/berichte/vergleich`): Monats- und Jahresbericht, Zeitraumvergleich.
4. **CSV-Export/-Import** für Transaktionen (Roadmap Phase 4, Excel-kompatibel mit `;`) und JSON-Komplettbackup.
5. **Projekte & Mitglieder** (`/projekte`): Projekte anlegen/umbenennen/löschen, Mitglieder per E-Mail einladen, Rollen (Leser/Bearbeiter/Admin) – im Cloud-Modus über Supabase.
6. **Login** (`/login`): E-Mail/Passwort, Registrierung, Magic-Link.
7. **Einstellungen** (`/einstellungen`): Profil, Darstellung, Backup/Restore, Demo-Daten zurücksetzen, Verbindungsstatus.
8. **Realtime**: Bei Änderungen anderer Projektmitglieder werden die Daten automatisch neu geladen (Supabase Realtime).

### Phase D – Qualität
* `dotnet build` ohne Fehler, Smoke-Test im Browser (Demo-Modus).
* Build-Artefakte (`obj/`) aus der Versionskontrolle entfernen (stehen bereits in `.gitignore`).

## 4. Supabase einrichten

Die vollständige Anleitung steht im [README](../README.md#-datenbank). Kurzfassung:

1. Projekt in Supabase anlegen.
2. Leere Datenbank: `database/setup_all.sql` ausführen. Bestehendes Alt-Schema: `supabase/migrations/0001_init.sql` ausführen (verlustfreier Umbau).
3. Lokal: URL und Publishable Key in `wwwroot/appsettings.Development.json` (nicht versioniert). Deployment: Repository-Secrets `SUPABASE_URL` und `SUPABASE_KEY`.
4. Unter *Authentication → URL Configuration* die App-URLs (lokal und z. B. `https://<user>.github.io/monexa/`) als Redirect-URLs eintragen.

## 5. Bewusst offen / nächste Schritte

* Open-Banking-Anbindung (PSD2/FinTS) – benötigt ein Backend, nicht im Client lösbar.
* KI-Kategorisierung – aktuell nur einfache Regel: Kategorievorschlag anhand früherer Buchungen mit gleicher Beschreibung.
* DATEV-Export – CSV ist vorbereitet, DATEV-Format benötigt Abstimmung mit Steuerberater.
* Fremdwährungen werden angezeigt, aber nicht umgerechnet (Summen gehen von EUR aus).
* Automatisierte Tests (bUnit) für `FinanceState`.
