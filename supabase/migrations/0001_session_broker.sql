create schema if not exists magmaedit;

create table if not exists magmaedit.desktop_sessions (
    user_id text primary key,
    session_id text not null unique,
    connection_id text not null unique,
    endpoint text not null,
    expires_at timestamptz not null,
    capabilities text[] not null default '{}'::text[],
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint desktop_sessions_user_id_not_blank check (length(btrim(user_id)) > 0),
    constraint desktop_sessions_session_id_not_blank check (length(btrim(session_id)) > 0),
    constraint desktop_sessions_connection_id_not_blank check (length(btrim(connection_id)) > 0),
    constraint desktop_sessions_endpoint_not_blank check (length(btrim(endpoint)) > 0)
);

create index if not exists desktop_sessions_expires_at_idx
    on magmaedit.desktop_sessions (expires_at);

comment on table magmaedit.desktop_sessions is
    'Authenticated MagmaEdit desktop session leases. Access is server-side only.';
