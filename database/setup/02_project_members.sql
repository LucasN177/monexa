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
