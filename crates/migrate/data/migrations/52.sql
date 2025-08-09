-- database version 52
-- messages db updates

create index messages_by_original on messages(original_mid);
create index messages_by_sender on messages(sender);

-- remove old table from database version 11
alter table command_messages rename to command_messages_old;

create table command_messages (
    mid bigint primary key,
    channel bigint not null,
    guild bigint not null,
    sender bigint not null,
    original_mid bigint not null
);

create index command_messages_by_original on command_messages(original_mid);
create index command_messages_by_sender on command_messages(sender);

update info set schema_version = 52;
