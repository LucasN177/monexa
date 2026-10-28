-- ════════════════════════════════════════════════════════════════════════════
-- MonExA – Datenbankschema für Supabase
--
-- Ausführen im Supabase-Dashboard unter SQL Editor (oder per `supabase db push`).
-- Das Skript ist idempotent und kann mehrfach ausgeführt werden:
--   • In einem leeren Projekt legt es alle Tabellen neu an.
--   • Ein bestehendes Alt-Schema (projects.project_id, accounts.account_id/account_name/balance,
--     transactions.transaction_id/category, project_members ohne id) wird verlustfrei umgebaut.
--
-- Enums werden als smallint gespeichert und entsprechen der Reihenfolge der
-- C#-Enums in Models/Enums (z. B. TransactionType: 0 = Einnahme, 1 = Ausgabe, 2 = Umbuchung).
-- ════════════════════════════════════════════════════════════════════════════

-- Alles oder nichts: schlägt ein Schritt fehl, bleibt die Datenbank unverändert.
begin;

create extension if not exists pgcrypto;

create or replace function pg_temp.has_table(t text) returns boolean language sql as $$
    select exists (select 1 from information_schema.tables where table_schema = 'public' and table_name = t);
$$;

create or replace function pg_temp.has_col(t text, c text) returns boolean language sql as $$
    select exists (select 1 from information_schema.columns where table_schema = 'public' and table_name = t and column_name = c);
$$;

-- Merkliste, welche Datenkonvertierungen in diesem Lauf nötig sind.
drop table if exists pg_temp._monexa_upgrade;
create temp table _monexa_upgrade (step text primary key);

-- ── 1. Alt-Schema umbenennen ────────────────────────────────────────────────

do $$
declare
    r record;
begin
    -- Alte Policies und Trigger entfernen (sie beziehen sich auf das alte Modell,
    -- z. B. Trigger, die accounts.balance fortschreiben – Salden berechnet jetzt die App).
    for r in
        select schemaname, tablename, policyname from pg_policies
         where schemaname = 'public'
           and tablename in ('projects', 'project_members', 'accounts', 'transactions', 'categories',
                             'budgets', 'recurring_transactions', 'savings_goals')
    loop
        execute format('drop policy %I on %I.%I', r.policyname, r.schemaname, r.tablename);
    end loop;

    for r in
        select t.tgname, c.relname from pg_trigger t join pg_class c on c.oid = t.tgrelid
         join pg_namespace n on n.oid = c.relnamespace
         where n.nspname = 'public' and not t.tgisinternal and t.tgname <> 'on_project_created'
           and c.relname in ('projects', 'project_members', 'accounts', 'transactions')
    loop
        raise notice 'Entferne Alt-Trigger % auf %', r.tgname, r.relname;
        execute format('drop trigger %I on public.%I', r.tgname, r.relname);
    end loop;

    -- projects
    if pg_temp.has_table('projects') then
        if pg_temp.has_col('projects', 'project_id') and not pg_temp.has_col('projects', 'id') then
            alter table public.projects rename column project_id to id;
        end if;
        if not pg_temp.has_col('projects', 'owner_id') then
            insert into _monexa_upgrade values ('projects_owner') on conflict do nothing;
        end if;
        if not exists (select 1 from pg_constraint where conrelid = 'public.projects'::regclass and contype = 'p') then
            alter table public.projects add primary key (id);
        end if;
    end if;

    -- project_members
    if pg_temp.has_table('project_members') then
        if exists (select 1 from information_schema.columns where table_schema = 'public' and table_name = 'project_members'
                    and column_name = 'role' and data_type in ('text', 'character varying')) then
            alter table public.project_members alter column role drop default;
            alter table public.project_members alter column role type smallint using (
                case
                    when lower(trim(role)) in ('admin', 'administrator', 'owner', 'eigentümer', 'eigentuemer', '2') then 2
                    when lower(trim(role)) in ('read', 'reader', 'viewer', 'leser', 'readonly', 'read_only', '0') then 0
                    else 1
                end);
        end if;
        if not pg_temp.has_col('project_members', 'id') then
            insert into _monexa_upgrade values ('members_convert') on conflict do nothing;
        end if;
    end if;

    -- accounts
    if pg_temp.has_table('accounts') then
        if pg_temp.has_col('accounts', 'account_id') and not pg_temp.has_col('accounts', 'id') then
            alter table public.accounts rename column account_id to id;
        end if;
        if pg_temp.has_col('accounts', 'account_name') and not pg_temp.has_col('accounts', 'name') then
            alter table public.accounts rename column account_name to name;
        end if;
        if not pg_temp.has_col('accounts', 'project_id') then
            insert into _monexa_upgrade values ('accounts_project') on conflict do nothing;
        end if;
        if not exists (select 1 from pg_constraint where conrelid = 'public.accounts'::regclass and contype = 'p') then
            alter table public.accounts add primary key (id);
        end if;
    end if;

    -- transactions
    if pg_temp.has_table('transactions') then
        if pg_temp.has_col('transactions', 'transaction_id') and not pg_temp.has_col('transactions', 'id') then
            alter table public.transactions rename column transaction_id to id;
        end if;
        if pg_temp.has_col('transactions', 'category') and not pg_temp.has_col('transactions', 'legacy_category') then
            alter table public.transactions rename column category to legacy_category;
        end if;
        if not pg_temp.has_col('transactions', 'type') then
            insert into _monexa_upgrade values ('tx_convert') on conflict do nothing;
        end if;
        if not exists (select 1 from pg_constraint where conrelid = 'public.transactions'::regclass and contype = 'p') then
            alter table public.transactions add primary key (id);
        end if;
    end if;

    -- Bisher war accounts.balance der aktuelle Kontostand. Neu wird der Anfangssaldo gespeichert und
    -- der aktuelle Stand aus den Buchungen berechnet – daher Summe der (vorzeichenbehafteten) Alt-Buchungen abziehen.
    if pg_temp.has_col('accounts', 'balance') and not pg_temp.has_col('accounts', 'opening_balance') then
        alter table public.accounts rename column balance to opening_balance;
        if pg_temp.has_col('transactions', 'account_id') and pg_temp.has_col('transactions', 'amount')
           and not pg_temp.has_col('transactions', 'type') then
            update public.accounts a
               set opening_balance = coalesce(a.opening_balance, 0)
                   - coalesce((select sum(t.amount) from public.transactions t where t.account_id = a.id), 0);
        end if;
    end if;
end;
$$;

-- ── 2. Tabellen (neu anlegen, falls nicht vorhanden) ────────────────────────

create table if not exists public.projects (
    id          uuid primary key default gen_random_uuid(),
    name        text        not null,
    icon        text        not null default 'Privat',
    owner_id    uuid        references auth.users (id) on delete set null default auth.uid(),
    created_at  timestamptz not null default now()
);

-- Mitgliedschaften und offene Einladungen (user_id = null bis zur Annahme).
-- role: 0 = Leser, 1 = Bearbeiter, 2 = Admin
create table if not exists public.project_members (
    id          uuid primary key default gen_random_uuid(),
    project_id  uuid        not null references public.projects (id) on delete cascade,
    user_id     uuid        references auth.users (id) on delete cascade,
    email       text        not null,
    role        smallint    not null default 0 check (role between 0 and 2),
    created_at  timestamptz not null default now(),
    constraint project_members_project_id_email_key unique (project_id, email)
);

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

-- ── 3. Fehlende Spalten in bestehenden Tabellen ergänzen ────────────────────

alter table public.projects
    add column if not exists icon       text        not null default 'Privat',
    add column if not exists owner_id   uuid        default auth.uid(),
    add column if not exists created_at timestamptz not null default now();

alter table public.project_members
    add column if not exists id         uuid        not null default gen_random_uuid(),
    add column if not exists email      text,
    add column if not exists created_at timestamptz not null default now();

alter table public.accounts
    add column if not exists project_id      uuid,
    add column if not exists type            smallint      not null default 0,
    add column if not exists iban            text          not null default '',
    add column if not exists opening_balance numeric(14,2) not null default 0,
    add column if not exists currency        text          not null default 'EUR',
    add column if not exists bank_name       text          not null default '',
    add column if not exists is_active       boolean       not null default true,
    add column if not exists description     text          not null default '',
    add column if not exists created_at      timestamptz   not null default now();

alter table public.transactions
    add column if not exists project_id    uuid,
    add column if not exists to_account_id uuid references public.accounts (id) on delete cascade,
    add column if not exists category_id   uuid references public.categories (id) on delete set null,
    add column if not exists description   text not null default '',
    add column if not exists date          date not null default current_date,
    add column if not exists type          smallint not null default 1,
    add column if not exists notes         text,
    add column if not exists recurring_id  uuid references public.recurring_transactions (id) on delete set null,
    add column if not exists created_at    timestamptz not null default now();

-- ── 4. Daten aus dem Alt-Schema überführen ──────────────────────────────────

do $$
declare
    r record;
    new_project uuid;
begin
    -- Mitglieder: E-Mail ergänzen, Primärschlüssel auf id umstellen, user_id auf auth.users beziehen
    if exists (select 1 from _monexa_upgrade where step = 'members_convert') then
        update public.project_members m
           set email = coalesce(m.email, u.email)
          from auth.users u
         where u.id = m.user_id and m.email is null;
        update public.project_members set email = coalesce(email, user_id::text, id::text) where email is null;

        for r in select conname from pg_constraint where conrelid = 'public.project_members'::regclass and contype = 'p' loop
            execute format('alter table public.project_members drop constraint %I', r.conname);
        end loop;
        alter table public.project_members add primary key (id);

        for r in
            select c.conname from pg_constraint c
              join pg_attribute a on a.attrelid = c.conrelid and a.attnum = any (c.conkey)
             where c.conrelid = 'public.project_members'::regclass and c.contype = 'f' and a.attname = 'user_id'
        loop
            execute format('alter table public.project_members drop constraint %I', r.conname);
        end loop;
        -- NOT VALID: bestehende Zeilen werden nicht geprüft (Alt-Daten könnten auf public.users zeigen).
        alter table public.project_members
            add constraint project_members_user_id_fkey foreign key (user_id) references auth.users (id) on delete cascade not valid;
    end if;

    -- Projekte: Eigentümer aus den Admin-Mitgliedschaften ableiten
    if exists (select 1 from _monexa_upgrade where step = 'projects_owner') then
        update public.projects p
           set owner_id = (select m.user_id from public.project_members m
                            where m.project_id = p.id and m.role = 2 and m.user_id is not null
                              and exists (select 1 from auth.users u where u.id = m.user_id)
                            order by m.created_at limit 1)
         where p.owner_id is null;
    end if;

    -- Konten: bisher pro Nutzer (owner_id), jetzt pro Projekt
    if exists (select 1 from _monexa_upgrade where step = 'accounts_project') then
        update public.accounts a
           set project_id = (select t.project_id from public.transactions t
                              where t.account_id = a.id and t.project_id is not null
                              group by t.project_id order by count(*) desc limit 1)
         where a.project_id is null;

        if pg_temp.has_col('accounts', 'owner_id') then
            update public.accounts a
               set project_id = (select m.project_id from public.project_members m
                                  where m.user_id = a.owner_id order by m.role desc, m.created_at limit 1)
             where a.project_id is null;

            -- Übrige Konten bekommen pro Eigentümer ein eigenes Projekt
            for r in select distinct owner_id from public.accounts where project_id is null loop
                insert into public.projects (name, icon, owner_id)
                values ('Privat', 'Privat', case when exists (select 1 from auth.users u where u.id = r.owner_id) then r.owner_id end)
                returning id into new_project;
                update public.accounts set project_id = new_project
                 where project_id is null and owner_id is not distinct from r.owner_id;
            end loop;
        end if;
    end if;

    -- Buchungen: Projekt ergänzen, Vorzeichen in Typ umwandeln, Datum aus created_at, Text-Kategorien übernehmen
    update public.transactions t
       set project_id = a.project_id
      from public.accounts a
     where a.id = t.account_id and t.project_id is null;

    if exists (select 1 from _monexa_upgrade where step = 'tx_convert') then
        update public.transactions
           set type   = case when amount < 0 then 1 else 0 end,
               amount = abs(amount),
               date   = coalesce(created_at::date, current_date);

        if pg_temp.has_col('transactions', 'legacy_category') then
            insert into public.categories (project_id, name, type)
            select distinct t.project_id, trim(t.legacy_category), t.type
              from public.transactions t
             where t.project_id is not null and coalesce(trim(t.legacy_category), '') <> ''
               and not exists (select 1 from public.categories c
                                where c.project_id = t.project_id and lower(c.name) = lower(trim(t.legacy_category)) and c.type = t.type);

            update public.transactions t
               set category_id = c.id
              from public.categories c
             where t.category_id is null and c.project_id = t.project_id and c.type = t.type
               and lower(c.name) = lower(trim(t.legacy_category));
        end if;
    end if;
end;
$$;

-- ── 5. Constraints & Defaults vereinheitlichen ──────────────────────────────

do $$
declare
    r record;
    known constant jsonb := '{
        "projects":        ["id","name","icon","owner_id","created_at"],
        "project_members": ["id","project_id","user_id","email","role","created_at"],
        "accounts":        ["id","project_id","name","type","iban","opening_balance","currency","bank_name","is_active","description","created_at"],
        "transactions":    ["id","project_id","account_id","to_account_id","category_id","description","amount","date","type","notes","recurring_id","created_at"]
    }';
begin
    -- Unbekannte Alt-Spalten dürfen Inserts der App nicht blockieren.
    for r in
        select c.table_name, c.column_name from information_schema.columns c
         where c.table_schema = 'public' and c.is_nullable = 'NO' and c.column_default is null
           and c.table_name in ('projects', 'project_members', 'accounts', 'transactions')
           and not (known -> c.table_name) ? c.column_name
    loop
        raise notice 'Alt-Spalte %.% ist jetzt optional', r.table_name, r.column_name;
        execute format('alter table public.%I alter column %I drop not null', r.table_name, r.column_name);
    end loop;

    -- Pflicht-Fremdschlüssel nur setzen, wenn alle Zeilen zugeordnet sind.
    if not exists (select 1 from public.accounts where project_id is null) then
        alter table public.accounts alter column project_id set not null;
    else
        raise warning 'Es gibt Konten ohne Projekt – diese sind in der App nicht sichtbar.';
    end if;
    if not exists (select 1 from public.transactions where project_id is null) then
        alter table public.transactions alter column project_id set not null;
    else
        raise warning 'Es gibt Buchungen ohne Projekt – diese sind in der App nicht sichtbar.';
    end if;
    -- Offene Einladungen haben noch keinen Nutzer (im Alt-Schema war user_id Teil des Primärschlüssels).
    alter table public.project_members alter column user_id drop not null;
    if not exists (select 1 from public.project_members where email is null) then
        alter table public.project_members alter column email set not null;
    end if;

    if not exists (select 1 from pg_constraint where conname = 'accounts_project_id_fkey') then
        alter table public.accounts
            add constraint accounts_project_id_fkey foreign key (project_id) references public.projects (id) on delete cascade;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'transactions_project_id_fkey') then
        alter table public.transactions
            add constraint transactions_project_id_fkey foreign key (project_id) references public.projects (id) on delete cascade;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'project_members_project_id_email_key') then
        alter table public.project_members
            add constraint project_members_project_id_email_key unique (project_id, email);
    end if;
end;
$$;

alter table public.projects        alter column id set default gen_random_uuid();
alter table public.project_members alter column id set default gen_random_uuid();
alter table public.accounts        alter column id set default gen_random_uuid();
alter table public.transactions    alter column id set default gen_random_uuid();
alter table public.projects        alter column owner_id set default auth.uid();

create index if not exists idx_members_user        on public.project_members (user_id);
create index if not exists idx_members_email       on public.project_members (lower(email));
create index if not exists idx_accounts_project    on public.accounts (project_id);
create index if not exists idx_categories_project  on public.categories (project_id);
create index if not exists idx_transactions_project_date on public.transactions (project_id, date desc);
create index if not exists idx_transactions_account on public.transactions (account_id);
create index if not exists idx_recurring_project   on public.recurring_transactions (project_id);
create index if not exists idx_budgets_project     on public.budgets (project_id);
create index if not exists idx_goals_project       on public.savings_goals (project_id);

-- ── 6. Rechteprüfung ────────────────────────────────────────────────────────

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

-- ── 7. Row Level Security ───────────────────────────────────────────────────

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

-- Mitglieder: alle Mitglieder sehen die Liste, nur Admins verwalten sie.
drop policy if exists "members_select" on public.project_members;
create policy "members_select" on public.project_members
    for select to authenticated using (public.project_role(project_id) is not null);

drop policy if exists "members_write" on public.project_members;
create policy "members_write" on public.project_members
    for all to authenticated
    using (public.project_role(project_id) = 2)
    with check (public.project_role(project_id) = 2);

-- Fachdaten: Lesen ab Rolle Leser, Schreiben ab Rolle Bearbeiter.
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

-- ── 8. Realtime ─────────────────────────────────────────────────────────────
-- Änderungen anderer Projektmitglieder werden live an die App gesendet.

do $$
declare
    t text;
begin
    if not exists (select 1 from pg_publication where pubname = 'supabase_realtime') then
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

drop table if exists pg_temp._monexa_upgrade;

commit;
