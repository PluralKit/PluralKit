-- schema version 26
-- cache Discord DM channels in the database

alter table accounts alter column system drop not null;
alter table accounts drop constraint accounts_system_fkey;
alter table accounts
    add constraint accounts_system_fkey
    foreign key (system) references systems(id) on delete set null;

alter table accounts add column dm_channel bigint;

update info set schema_version = 26;