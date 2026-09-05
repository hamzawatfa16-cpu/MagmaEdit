create table if not exists magmaedit.broker_credentials (
    token_hash text primary key,
    user_id text not null,
    expires_at timestamptz not null,
    revoked_at timestamptz null,
    created_at timestamptz not null default timezone('utc', now()),
    constraint broker_credentials_token_hash_not_blank check (length(btrim(token_hash)) > 0),
    constraint broker_credentials_user_id_not_blank check (length(btrim(user_id)) > 0)
);

create index if not exists broker_credentials_user_id_idx
    on magmaedit.broker_credentials (user_id);

create index if not exists broker_credentials_expires_at_idx
    on magmaedit.broker_credentials (expires_at);

create table if not exists magmaedit.broker_replay_requests (
    request_id text primary key,
    accepted_at timestamptz not null,
    constraint broker_replay_requests_request_id_not_blank check (length(btrim(request_id)) > 0)
);

create index if not exists broker_replay_requests_accepted_at_idx
    on magmaedit.broker_replay_requests (accepted_at);

alter table magmaedit.broker_credentials enable row level security;
alter table magmaedit.broker_replay_requests enable row level security;

comment on table magmaedit.broker_credentials is
    'Hashed short-lived MagmaEdit broker credentials. Access is server-side only.';

comment on table magmaedit.broker_replay_requests is
    'One-time MagmaEdit broker request identifiers used for replay protection. Access is server-side only.';
