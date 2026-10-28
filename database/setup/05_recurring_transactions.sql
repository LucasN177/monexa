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
