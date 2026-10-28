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
