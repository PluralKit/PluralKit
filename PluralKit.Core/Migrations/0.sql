-- SCHEMA VERSION 0, 2019-12-26
-- "initial version", considered a "starting point" for the migrations

-- also the assumed database layout of someone either migrating from an older version of PK or starting a new instance,
-- so everything here *should* be idempotent given a schema version older than this or nonexistent.

-- Create proxy_tag compound type if it doesn't exist
do $$ begin
    create type proxy_tag as (
        prefix text,
        suffix text
    );
exception when duplicate_object then null;
end $$;

create table if not exists systems
(
    id          serial primary key,
    hid         char(5) unique not null,
    name        text,
    description text,
    tag         text,
    avatar_url  text,
    token       text,
    created     timestamp      not null default (current_timestamp at time zone 'utc'),
    ui_tz       text           not null default 'UTC'
);

create table if not exists system_guild
(
    system serial not null references systems (id) on delete cascade,
    guild bigint not null,
    
    proxy_enabled bool not null default true,
    
    primary key (system, guild)
);

create table if not exists members
(
    id           serial primary key,
    hid          char(5) unique not null,
    system       serial         not null references systems (id) on delete cascade,
    color        char(6),
    avatar_url   text,
    name         text           not null,
    display_name text,
    birthday     date,
    pronouns     text,
    description  text,
    proxy_tags   proxy_tag[]    not null default array[]::proxy_tag[], -- Rationale on making this an array rather than a separate table - we never need to query them individually, only access them as part of a selected Member struct
    keep_proxy   bool           not null default false, 
    created      timestamp      not null default (current_timestamp at time zone 'utc')
);

create table if not exists member_guild
(
    member serial not null references members (id) on delete cascade,
    guild bigint not null,
    
    display_name text default null,
    
    primary key (member, guild)
);

create table if not exists accounts
(
    uid    bigint primary key,
    system serial not null references systems (id) on delete cascade
);

create table if not exists messages
(
    mid          bigint primary key,
    channel      bigint not null,
    member       serial not null references members (id) on delete cascade,
    sender       bigint not null,
    original_mid bigint
);

create table if not exists switches
(
    id        serial primary key,
    system    serial    not null references systems (id) on delete cascade,
    timestamp timestamp not null default (current_timestamp at time zone 'utc')
);

create table if not exists switch_members
(
    id     serial primary key,
    switch serial not null references switches (id) on delete cascade,
    member serial not null references members (id) on delete cascade
);

create table if not exists webhooks
(
    channel bigint primary key,
    webhook bigint not null,
    token   text   not null
);

create table if not exists servers
(
    id            bigint primary key,
    log_channel   bigint,
    log_blacklist bigint[] not null default array[]::bigint[],
    blacklist     bigint[] not null default array[]::bigint[] 
);

create index if not exists idx_switches_system on switches using btree (system asc nulls last) include ("timestamp");
create index if not exists idx_switch_members_switch on switch_members using btree (switch asc nulls last) include (member);
create index if not exists idx_message_member on messages (member);
