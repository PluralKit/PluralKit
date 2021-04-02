-- new autoproxy format
-- allows for global/channel autoproxy, and "un-latching"

-- scope pseudo-enum:
-- 1: global autoproxy
-- 2: guild autoproxy (default, current behaviour)
-- 3: channel autoproxy

-- mode pseudo-enum: (copied from 3.sql)
-- 1 = autoproxy off
-- 2 = front mode (first fronter)
-- 3 = latch mode (last proxyer)
-- 4 = member mode (specific member)

create table autoproxy
(
    system int not null references systems(id) on delete cascade,
    scope int check (scope in (1, 2, 3)) not null default 2,
    mode int check (mode in (1, 2, 3, 4)) not null default 1,
    location bigint,
    member int references members(id) on delete set null,
    primary key (system, location)
);

-- TODO: latch timestamp + update trigger

-- TODO: migrate existing autoproxy data
-- none of this code works lol

-- with
--     data as (select system, autoproxy_mode, autoproxy_member, guild from system_guild)
-- insert into autoproxy 
--     (system, scope, mode, member, location) values
--     (data.system, 2, data.autoproxy_mode, data.autoproxy_member, data.guild);

-- with
--     data as (select system, guild from system_guild where autoproxy_mode = 3),
-- update autoproxy set 
--     member = (select member from messages where messages.guild = data.guild order by mid limit 1)
-- where 
--     system = data.system and location = data.guild and autoproxy_mode = 3;

-- with data as (select system, guild, autoproxy_member from system_guild where autoproxy_mode = 4)
--     update autoproxy set member = autoproxy_member where autoproxy.mode = 4 and location = data.guild;

-- drop old columns

alter table system_guild drop column autoproxy_mode;
alter table system_guild drop column autoproxy_member;

update info set schema_version = 15;