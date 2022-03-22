-- schema version 27
-- autoproxy locations

-- mode pseudo-enum: (copied from 3.sql)
-- 1 = autoproxy off
-- 2 = front mode (first fronter)
-- 3 = latch mode (last proxyer)
-- 4 = member mode (specific member)

create table autoproxy (
    system int references systems(id) on delete cascade,
    channel_id bigint,
    guild_id bigint,
    autoproxy_mode int check (autoproxy_mode in (1, 2, 3, 4)) not null default 1,
    autoproxy_member int references members(id) on delete set null,
    last_latch_timestamp timestamp,
    check (
        (channel_id  = 0 and guild_id  = 0)
     or (channel_id != 0 and guild_id  = 0)
     or (channel_id  = 0 and guild_id != 0)
    ),
    primary key (system, channel_id, guild_id)
);

insert into autoproxy select
    system,
    0 as channel_id,
    guild as guild_id,
    autoproxy_mode,
    autoproxy_member
from system_guild;

alter table system_guild drop column autoproxy_mode;
alter table system_guild drop column autoproxy_member;

update info set schema_version = 27;