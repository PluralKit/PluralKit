-- SCHEMA VERSION 9: 2020-xx-xx --
-- Adds support for member groups.

create table groups (
    id int primary key generated always as identity,
    hid char(5) unique not null,
    system int not null references systems(id) on delete cascade,
    
    name text not null,
    description text,
    icon text,
    
    -- Description columns follow the same pattern as usual: 1 = public, 2 = private
    description_privacy integer check (description_privacy in (1, 2)) not null default 1,
    icon_privacy integer check (icon_privacy in (1, 2)) not null default 1,
    visibility integer check (visibility in (1, 2)) not null default 1,

    created timestamp with time zone not null default (current_timestamp at time zone 'utc')
);

create table group_members (
    group_id int not null references groups(id) on delete cascade,
    member_id int not null references members(id) on delete cascade
);

update info set schema_version = 9;
