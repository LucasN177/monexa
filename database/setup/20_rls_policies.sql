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
