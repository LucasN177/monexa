-- ════════════════════════════════════════════════════════════════════════════
-- MonExA – komplette Datenbank in einem Schritt einrichten
--
-- GENERIERT von database/build-setup-all.ps1 – nicht direkt bearbeiten,
-- sondern die Einzelskripte in database/setup/ ändern und neu erzeugen.
--
-- Für eine leere Supabase-Datenbank: Inhalt in den SQL-Editor einfügen und ausführen.
-- Läuft als eine Transaktion und kann gefahrlos mehrfach ausgeführt werden.
-- ════════════════════════════════════════════════════════════════════════════

begin;

-- >>> setup/00_extensions.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 00 – Erweiterungen
-- gen_random_uuid() für Primärschlüssel (auf Supabase meist schon aktiv).
-- ════════════════════════════════════════════════════════════════════════════

create extension if not exists pgcrypto;

-- >>> setup/01_projects.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 01 – Projekte (Mandanten). Alle Fachdaten hängen an genau einem Projekt.
-- C#-Modell: Models/Project.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.projects (
    id          uuid primary key default gen_random_uuid(),
    name        text        not null,
    icon        text        not null default 'Privat',
    owner_id    uuid        references auth.users (id) on delete set null default auth.uid(),
    created_at  timestamptz not null default now()
);

-- >>> setup/02_project_members.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 02 – Projektmitglieder und offene Einladungen
-- user_id ist null, solange die Einladung nicht angenommen wurde.
-- role: 0 = Leser, 1 = Bearbeiter, 2 = Admin   (Models/Enums/ProjectRole.cs)
-- C#-Modell: Models/ProjectMember.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.project_members (
    id          uuid primary key default gen_random_uuid(),
    project_id  uuid        not null references public.projects (id) on delete cascade,
    user_id     uuid        references auth.users (id) on delete cascade,
    email       text        not null,
    role        smallint    not null default 0 check (role between 0 and 2),
    created_at  timestamptz not null default now(),
    constraint project_members_project_id_email_key unique (project_id, email)
);

create index if not exists idx_members_user  on public.project_members (user_id);
create index if not exists idx_members_email on public.project_members (lower(email));

-- >>> setup/03_accounts.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 03 – Konten
-- type: 0 = Girokonto, 1 = Sparkonto, 2 = Tagesgeld, 3 = Kreditkarte, 4 = Depot
--       (Models/Enums/AccountType.cs)
-- opening_balance ist der Saldo vor der ersten Buchung; der aktuelle Kontostand
-- wird in der App aus opening_balance + Buchungen berechnet.
-- C#-Modell: Models/Konto.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.accounts (
    id              uuid primary key default gen_random_uuid(),
    project_id      uuid          not null references public.projects (id) on delete cascade,
    name            text          not null,
    type            smallint      not null default 0,
    iban            text          not null default '',
    opening_balance numeric(14,2) not null default 0,
    currency        text          not null default 'EUR',
    bank_name       text          not null default '',
    is_active       boolean       not null default true,
    description     text          not null default '',
    created_at      timestamptz   not null default now()
);

create index if not exists idx_accounts_project on public.accounts (project_id);

-- >>> setup/04_categories.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 04 – Kategorien
-- type: 0 = Einnahme, 1 = Ausgabe   (Models/Enums/CategoryType.cs)
-- icon: Schlüssel aus UiHelpers.CategoryIcons, color: MudBlazor-Farbname (z. B. "Primary")
-- C#-Modell: Models/Category.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.categories (
    id          uuid primary key default gen_random_uuid(),
    project_id  uuid        not null references public.projects (id) on delete cascade,
    name        text        not null,
    description text,
    type        smallint    not null default 1,
    icon        text        not null default 'Sonstiges',
    color       text        not null default 'Primary',
    is_active   boolean     not null default true,
    sort_order  integer     not null default 0,
    created_at  timestamptz not null default now()
);

create index if not exists idx_categories_project on public.categories (project_id);

-- >>> setup/05_recurring_transactions.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 05 – Daueraufträge / Abos
-- type:     0 = Einnahme, 1 = Ausgabe, 2 = Umbuchung   (Models/Enums/TransactionType.cs)
-- interval: 0 = Wöchentlich, 1 = Monatlich, 2 = Quartalsweise, 3 = Halbjährlich, 4 = Jährlich
--           (Models/Enums/RecurrenceInterval.cs)
-- Muss vor 06_transactions.sql angelegt werden (transactions.recurring_id verweist hierher).
-- C#-Modell: Models/RecurringTransaction.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.recurring_transactions (
    id            uuid primary key default gen_random_uuid(),
    project_id    uuid          not null references public.projects (id) on delete cascade,
    account_id    uuid          not null references public.accounts (id) on delete cascade,
    to_account_id uuid          references public.accounts (id) on delete set null,
    category_id   uuid          references public.categories (id) on delete set null,
    description   text          not null,
    amount        numeric(14,2) not null check (amount >= 0),
    type          smallint      not null default 1,
    interval      smallint      not null default 1,
    next_due      date          not null,
    end_date      date,
    auto_book     boolean       not null default true,
    is_active     boolean       not null default true,
    created_at    timestamptz   not null default now()
);

create index if not exists idx_recurring_project on public.recurring_transactions (project_id);

-- >>> setup/06_transactions.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 06 – Buchungen
-- type: 0 = Einnahme, 1 = Ausgabe, 2 = Umbuchung   (Models/Enums/TransactionType.cs)
-- amount ist immer positiv, das Vorzeichen ergibt sich aus type.
-- Bei Umbuchungen ist to_account_id das Zielkonto.
-- C#-Modell: Models/Transaction.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.transactions (
    id            uuid primary key default gen_random_uuid(),
    project_id    uuid          not null references public.projects (id) on delete cascade,
    account_id    uuid          not null references public.accounts (id) on delete cascade,
    to_account_id uuid          references public.accounts (id) on delete cascade,
    category_id   uuid          references public.categories (id) on delete set null,
    description   text          not null,
    amount        numeric(14,2) not null check (amount >= 0),
    date          date          not null,
    type          smallint      not null default 1,
    notes         text,
    recurring_id  uuid          references public.recurring_transactions (id) on delete set null,
    created_at    timestamptz   not null default now()
);

create index if not exists idx_transactions_project_date on public.transactions (project_id, date desc);
create index if not exists idx_transactions_account      on public.transactions (account_id);

-- >>> setup/07_budgets.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 07 – Budgets
-- period: 0 = Wöchentlich, 1 = Monatlich, 2 = Jährlich   (Models/Enums/BudgetPeriod.cs)
-- category_id = null bedeutet: Limit gilt für alle Ausgaben.
-- C#-Modell: Models/Budget.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.budgets (
    id           uuid primary key default gen_random_uuid(),
    project_id   uuid          not null references public.projects (id) on delete cascade,
    category_id  uuid          references public.categories (id) on delete cascade,
    name         text          not null,
    limit_amount numeric(14,2) not null check (limit_amount >= 0),
    period       smallint      not null default 1,
    icon         text          not null default 'Shopping',
    color        text          not null default 'Primary',
    created_at   timestamptz   not null default now()
);

create index if not exists idx_budgets_project on public.budgets (project_id);

-- >>> setup/08_savings_goals.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 08 – Sparziele
-- C#-Modell: Models/SavingsGoal.cs
-- ════════════════════════════════════════════════════════════════════════════

create table if not exists public.savings_goals (
    id             uuid primary key default gen_random_uuid(),
    project_id     uuid          not null references public.projects (id) on delete cascade,
    name           text          not null,
    target_amount  numeric(14,2) not null check (target_amount >= 0),
    current_amount numeric(14,2) not null default 0 check (current_amount >= 0),
    target_date    date,
    icon           text          not null default 'Sparen',
    color          text          not null default 'Success',
    notes          text,
    created_at     timestamptz   not null default now()
);

create index if not exists idx_goals_project on public.savings_goals (project_id);

-- >>> setup/10_functions.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 10 – Funktionen & Trigger
--   project_role(project)          Rolle des angemeldeten Nutzers (Basis aller Policies)
--   handle_new_project()           trägt den Eigentümer eines neuen Projekts als Admin ein
--   accept_pending_invitations()   übernimmt Einladungen für die eigene E-Mail (Aufruf durch die App)
-- ════════════════════════════════════════════════════════════════════════════

-- Rolle des angemeldeten Nutzers im Projekt (2 = Admin/Eigentümer, null = kein Zugriff).
-- SECURITY DEFINER, damit die Policies nicht rekursiv auf project_members prüfen müssen.
create or replace function public.project_role(p_project uuid)
returns smallint
language sql
stable
security definer
set search_path = public
as $$
    select case
        when exists (select 1 from projects where id = p_project and owner_id = auth.uid()) then 2::smallint
        else (select role from project_members where project_id = p_project and user_id = auth.uid() limit 1)
    end;
$$;

revoke all on function public.project_role(uuid) from public;
grant execute on function public.project_role(uuid) to authenticated;

-- Eigentümer automatisch als Admin eintragen.
create or replace function public.handle_new_project()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if new.owner_id is not null then
        insert into project_members (project_id, user_id, email, role)
        values (new.id, new.owner_id,
                coalesce(auth.jwt() ->> 'email', (select email from auth.users where id = new.owner_id), new.owner_id::text), 2)
        on conflict (project_id, email) do nothing;
    end if;
    return new;
end;
$$;

drop trigger if exists on_project_created on public.projects;
create trigger on_project_created
    after insert on public.projects
    for each row execute function public.handle_new_project();

-- Offene Einladungen für die E-Mail des angemeldeten Nutzers annehmen (wird beim Start der App aufgerufen).
create or replace function public.accept_pending_invitations()
returns integer
language plpgsql
security definer
set search_path = public
as $$
declare
    accepted integer;
begin
    update project_members
       set user_id = auth.uid()
     where user_id is null
       and lower(email) = lower(auth.jwt() ->> 'email');
    get diagnostics accepted = row_count;
    return accepted;
end;
$$;

revoke all on function public.accept_pending_invitations() from public;
grant execute on function public.accept_pending_invitations() to authenticated;

-- >>> setup/20_rls_policies.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 20 – Row Level Security
--   Projekte:    sehen = Mitglied, anlegen = jeder (als Eigentümer), ändern/löschen = Admin
--   Mitglieder:  sehen = Mitglied, verwalten = Admin
--   Fachdaten:   lesen ab Leser (0), schreiben ab Bearbeiter (1)
-- ════════════════════════════════════════════════════════════════════════════

alter table public.projects               enable row level security;
alter table public.project_members        enable row level security;
alter table public.accounts               enable row level security;
alter table public.categories             enable row level security;
alter table public.recurring_transactions enable row level security;
alter table public.transactions           enable row level security;
alter table public.budgets                enable row level security;
alter table public.savings_goals          enable row level security;

grant select, insert, update, delete on
    public.projects, public.project_members, public.accounts, public.categories,
    public.recurring_transactions, public.transactions, public.budgets, public.savings_goals
    to authenticated;

-- Projekte
drop policy if exists "projects_select" on public.projects;
create policy "projects_select" on public.projects
    for select to authenticated using (owner_id = auth.uid() or public.project_role(id) is not null);

drop policy if exists "projects_insert" on public.projects;
create policy "projects_insert" on public.projects
    for insert to authenticated with check (owner_id = auth.uid());

drop policy if exists "projects_update" on public.projects;
create policy "projects_update" on public.projects
    for update to authenticated using (public.project_role(id) = 2) with check (public.project_role(id) = 2);

drop policy if exists "projects_delete" on public.projects;
create policy "projects_delete" on public.projects
    for delete to authenticated using (public.project_role(id) = 2);

-- Mitglieder
drop policy if exists "members_select" on public.project_members;
create policy "members_select" on public.project_members
    for select to authenticated using (public.project_role(project_id) is not null);

drop policy if exists "members_write" on public.project_members;
create policy "members_write" on public.project_members
    for all to authenticated
    using (public.project_role(project_id) = 2)
    with check (public.project_role(project_id) = 2);

-- Fachdaten
do $$
declare
    t text;
begin
    foreach t in array array['accounts', 'categories', 'recurring_transactions', 'transactions', 'budgets', 'savings_goals']
    loop
        execute format('drop policy if exists "%1$s_select" on public.%1$I', t);
        execute format('create policy "%1$s_select" on public.%1$I for select to authenticated
                        using (public.project_role(project_id) is not null)', t);

        execute format('drop policy if exists "%1$s_write" on public.%1$I', t);
        execute format('create policy "%1$s_write" on public.%1$I for all to authenticated
                        using (public.project_role(project_id) >= 1)
                        with check (public.project_role(project_id) >= 1)', t);
    end loop;
end;
$$;

-- >>> setup/30_realtime.sql
-- ════════════════════════════════════════════════════════════════════════════
-- 30 – Realtime
-- Änderungen anderer Projektmitglieder werden live an die App gesendet
-- (Services/RealtimeSync.cs). Ohne Publication (z. B. reines Postgres) wird übersprungen.
-- ════════════════════════════════════════════════════════════════════════════

do $$
declare
    t text;
begin
    if not exists (select 1 from pg_publication where pubname = 'supabase_realtime') then
        raise notice 'Publication supabase_realtime nicht vorhanden – Realtime wird übersprungen.';
        return;
    end if;
    foreach t in array array['accounts', 'categories', 'recurring_transactions', 'transactions', 'budgets', 'savings_goals', 'project_members']
    loop
        if not exists (
            select 1 from pg_publication_tables
             where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = t
        ) then
            execute format('alter publication supabase_realtime add table public.%I', t);
        end if;
    end loop;
end;
$$;

commit;
