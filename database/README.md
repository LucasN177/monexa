# Datenbank-Skripte

Alle Skripte, um die MonExA-Datenbank auf einem **neuen, leeren** Supabase-Projekt
(oder Postgres mit Supabase-Auth) komplett einzurichten.

| Datei | Inhalt |
|---|---|
| `setup/00_extensions.sql` | Erweiterung `pgcrypto` (UUIDs) |
| `setup/01_projects.sql` | Projekte (Mandanten) |
| `setup/02_project_members.sql` | Mitglieder, Rollen, offene Einladungen |
| `setup/03_accounts.sql` | Konten |
| `setup/04_categories.sql` | Kategorien |
| `setup/05_recurring_transactions.sql` | Daueraufträge / Abos |
| `setup/06_transactions.sql` | Buchungen |
| `setup/07_budgets.sql` | Budgets |
| `setup/08_savings_goals.sql` | Sparziele |
| `setup/10_functions.sql` | Rechteprüfung, Eigentümer-Trigger, Einladungen annehmen |
| `setup/20_rls_policies.sql` | Row Level Security |
| `setup/30_realtime.sql` | Live-Synchronisation (Supabase Realtime) |
| `setup_all.sql` | **Alles in einer Datei** – aus den Einzelskripten generiert |

Die Nummern geben die Reihenfolge vor (Fremdschlüssel). Alle Skripte sind idempotent
(`if not exists`, `create or replace`) und können mehrfach ausgeführt werden.

## Neu aufsetzen

**Supabase-Dashboard:** *SQL Editor* → Inhalt von `setup_all.sql` einfügen → *Run*.
Das Skript läuft als eine Transaktion – schlägt etwas fehl, bleibt die Datenbank unverändert.

**Per `psql`** (Verbindungsdaten unter *Project Settings → Database*):

```bash
psql "postgresql://postgres:<passwort>@db.<projekt>.supabase.co:5432/postgres" -v ON_ERROR_STOP=1 -f database/setup_all.sql
```

Danach in der App bzw. im Deployment URL und Publishable Key setzen
(lokal `wwwroot/appsettings.Development.json`, GitHub-Secrets `SUPABASE_URL` / `SUPABASE_KEY`)
und unter *Authentication → URL Configuration* die App-URLs als Redirect-URLs eintragen.

## Skripte ändern

1. Das passende Einzelskript in `setup/` anpassen (bei neuen Spalten auch das C#-Modell in `Models/`).
2. `setup_all.sql` neu erzeugen:
   ```powershell
   powershell -ExecutionPolicy Bypass -File database/build-setup-all.ps1
   ```
3. Für eine **bestehende** Datenbank zusätzlich ein Änderungsskript unter `supabase/migrations/`
   anlegen (z. B. `0002_….sql` mit `alter table … add column if not exists …`).

## Abgrenzung zu `supabase/migrations/`

`supabase/migrations/0001_init.sql` ist das Upgrade-Skript für die bereits vorhandene Datenbank:
Es baut das alte Schema (`project_id`, `account_name`, `balance`, …) verlustfrei auf das
neue Modell um. Für eine frische Installation reicht `setup_all.sql`; beide führen zum
gleichen Endzustand.
