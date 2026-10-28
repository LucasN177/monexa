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
