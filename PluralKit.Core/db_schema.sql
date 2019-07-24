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
    id          serial primary key,
    hid         char(5) unique not null,
    system      serial         not null references systems (id) on delete cascade,
    color       char(6),
    avatar_url  text,
    name        text           not null,
    birthday    date,
    pronouns    text,
    description text,
    prefix      text,
    suffix      text,
    created     timestamp      not null default (current_timestamp at time zone 'utc')
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
    id          bigint primary key,
    log_channel bigint
);