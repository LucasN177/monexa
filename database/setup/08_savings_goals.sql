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
