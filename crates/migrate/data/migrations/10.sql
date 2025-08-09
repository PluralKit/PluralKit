-- SCHEMA VERSION 10: 2020-10-09 --
-- Member/group limit override per-system

alter table systems add column member_limit_override smallint default null;
alter table systems add column group_limit_override smallint default null;

-- Lowering global limit to 1000 in this commit, so increase it for systems already above that
update systems s set member_limit_override = 1500
    where (select count(*) from members m where m.system = s.id) > 1000;

update info set schema_version = 10;