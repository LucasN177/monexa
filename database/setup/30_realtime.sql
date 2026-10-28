-- ════════════════════════════════════════════════════════════════════════════
-- 30 – Realtime
-- Änderungen anderer Projektmitglieder werden live an die App gesendet
-- (Services/RealtimeSync.cs). Ohne Publication (z. B. reines Postgres) wird übersprungen.
-- ════════════════════════════════════════════════════════════════════════════

do $$
declare
    t text;
begin
    if not exists (select 1 from pg_publication where pubname = 'supabase_realtime') then
        raise notice 'Publication supabase_realtime nicht vorhanden – Realtime wird übersprungen.';
        return;
    end if;
    foreach t in array array['accounts', 'categories', 'recurring_transactions', 'transactions', 'budgets', 'savings_goals', 'project_members']
    loop
        if not exists (
            select 1 from pg_publication_tables
             where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = t
        ) then
            execute format('alter publication supabase_realtime add table public.%I', t);
        end if;
    end loop;
end;
$$;
