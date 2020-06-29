-- SCHEMA VERSION 9: 2020-xx-xx --

create table groups (
    id int primary key generated always as identity,
    hid char(5) unique not null,
    system int not null references systems(id) on delete cascade,
    name text not null,
    description text,
    created timestamp with time zone not null default (current_timestamp at time zone 'utc')
);

create table group_members (
    group_id int not null references groups(id) on delete cascade,
    member_id int not null references members(id) on delete cascade
);

update info set schema_version = 9;
