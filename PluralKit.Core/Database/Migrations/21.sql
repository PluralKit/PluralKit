-- schema version 21
-- create `system_config` table

create table system_config (
    system int primary key references systems(id) on delete cascade,
    ui_tz text not null default 'UTC',
    pings_enabled bool not null default true,
    latch_timeout int,
    member_limit_override int,
    group_limit_override int
);

insert into system_config select
    id as system,
    ui_tz,
    pings_enabled,
    latch_timeout,
    member_limit_override,
    group_limit_override
from systems;

alter table systems
    drop column ui_tz,
    drop column pings_enabled,
    drop column latch_timeout,
    drop column member_limit_override,
    drop column group_limit_override;

update info set schema_version = 21;
