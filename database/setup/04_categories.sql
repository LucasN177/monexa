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
