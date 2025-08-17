-- database version 53
--
-- scoped API keys + skeleton for oauth2 for third-party apps

create table external_apps (
    id uuid primary key default gen_random_uuid(),
    name text not null,
    homepage_url text not null,
    oauth2_secret text,
    oauth2_allowed_redirects text[] not null default array[]::text[],
    oauth2_scopes text[] not null default array[]::text[],
    api_rl_token text,
    api_rl_rate int
);

create type api_key_type as enum (
    'dashboard',
    'user_created',
    'external_app'
);

create table api_keys (
    id uuid primary key default gen_random_uuid(),
    system int references systems(id) on delete cascade,
    kind api_key_type not null,
    scopes text[] not null default array[]::text[],
    app uuid references external_apps(id) on delete cascade,
    name text,

    discord_id bigint,
    discord_access_token text,
    discord_refresh_token text,
    discord_expires_at timestamp,

    created timestamp with time zone not null default (current_timestamp at time zone 'utc')
);

update info set schema_version = 53;
