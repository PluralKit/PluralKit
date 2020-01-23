-- Same sort of psuedo-enum due to Dapper limitations. See 2.sql.
-- 1 = autoproxy off
-- 2 = front mode (first fronter)
-- 3 = latch mode (last proxyer)
-- 4 = member mode (specific member)
alter table system_guild add column autoproxy_mode int check (autoproxy_mode in (1, 2, 3, 4)) not null default 1;

-- for member mode
alter table system_guild add column autoproxy_member int references members (id) on delete set null;

-- for latch mode
-- not *really* nullable, null just means old (pre-schema-change) data.
alter table messages add column guild bigint default null;

update info set schema_version = 3;