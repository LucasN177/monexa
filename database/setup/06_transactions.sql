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
