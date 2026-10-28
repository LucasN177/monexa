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
