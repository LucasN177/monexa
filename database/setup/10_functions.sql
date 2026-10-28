-- ════════════════════════════════════════════════════════════════════════════
-- 10 – Funktionen & Trigger
--   project_role(project)          Rolle des angemeldeten Nutzers (Basis aller Policies)
--   handle_new_project()           trägt den Eigentümer eines neuen Projekts als Admin ein
--   accept_pending_invitations()   übernimmt Einladungen für die eigene E-Mail (Aufruf durch die App)
-- ════════════════════════════════════════════════════════════════════════════

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
