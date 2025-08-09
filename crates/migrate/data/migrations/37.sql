-- database version 37
-- add proxy tag privacy

alter table members add column proxy_privacy integer not null default 1;
alter table members add constraint members_proxy_privacy_check check (proxy_privacy = ANY (ARRAY[1,2]));

update info set schema_version = 37;