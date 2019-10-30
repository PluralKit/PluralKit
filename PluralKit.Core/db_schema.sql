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
    proxy_tags   proxy_tag[]    not null default array[], -- Rationale on making this an array rather than a separate table - we never need to query them individually, only access them as part of a selected Member struct
    keep_proxy   bool           not null default false, 
    created      timestamp      not null default (current_timestamp at time zone 'utc')
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
CREATE INDEX IF NOT EXISTS idx_switches_system
ON switches USING btree (
	system ASC NULLS LAST
) INCLUDE ("timestamp");

create table if not exists switch_members
(
    id     serial primary key,
    switch serial not null references switches (id) on delete cascade,
    member serial not null references members (id) on delete cascade
);
CREATE INDEX IF NOT EXISTS idx_switch_members_switch
ON switch_members USING btree (
	switch ASC NULLS LAST
) INCLUDE (member);

create table if not exists webhooks
(
    channel bigint primary key,
    webhook bigint not null,
    token   text   not null
);

create table if not exists servers
(
    id          bigint primary key,
    log_channel bigint
);

/* UPDATES */

--Add proxy_tags column to members
ALTER TABLE members ADD COLUMN IF NOT EXISTS proxy_tags proxy_tag[] NOT NULL DEFAULT array[]::proxy_tag[];

--Migrate legacy proxy tags to proxy_tags array
DO $$ BEGIN
	IF EXISTS (
		SELECT column_name 
		FROM information_schema.columns 
		WHERE table_schema = 'public' AND table_name = 'members' and column_name = 'prefix'
	)
	AND EXISTS (
		SELECT column_name 
		FROM information_schema.columns 
		WHERE table_schema = 'public' AND table_name = 'members' and column_name = 'suffix'
	)
	THEN
		UPDATE members
		SET proxy_tags = ARRAY[(prefix, suffix)]::proxy_tag[]
		WHERE prefix IS NOT NULL
		OR suffix IS NOT NULL;

		ALTER TABLE members DROP COLUMN prefix;
		ALTER TABLE members DROP COLUMN suffix;
	END IF;
END $$;

--Add keep_proxy column to members
ALTER TABLE members ADD COLUMN IF NOT EXISTS keep_proxy bool NOT NULL DEFAULT false;