-- ════════════════════════════════════════════════════════════════════════════
-- 00 – Erweiterungen
-- gen_random_uuid() für Primärschlüssel (auf Supabase meist schon aktiv).
-- ════════════════════════════════════════════════════════════════════════════

create extension if not exists pgcrypto;
