-- We're doing a psuedo-enum here since Dapper is wonky with enums
-- Still getting mapped to enums at the CLR level, though.
-- https://github.com/StackExchange/Dapper/issues/332 (from 2015, still unsolved!)
-- 1 = "public"
-- 2 = "private"
-- not doing a bool here since I want to open up for the possibliity of other privacy levels (eg. "mutuals only")
alter table systems add column description_privacy integer check (description_privacy in (1, 2)) not null default 1;
alter table systems add column member_list_privacy integer check (member_list_privacy in (1, 2)) not null default 1;
alter table systems add column front_privacy integer check (front_privacy in (1, 2)) not null default 1;
alter table systems add column front_history_privacy integer check (front_history_privacy in (1, 2)) not null default 1;
alter table members add column member_privacy integer check (member_privacy in (1, 2)) not null default 1;

update info set schema_version = 2;