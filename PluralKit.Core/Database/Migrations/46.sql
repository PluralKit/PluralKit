-- database version 46
-- adds banner privacy

alter table members add column banner_privacy int not null default 1 check (banner_privacy = ANY (ARRAY[1,2]));
alter table groups add column banner_privacy int not null default 1 check (banner_privacy = ANY (ARRAY[1,2]));
alter table systems add column banner_privacy int not null default 1 check (banner_privacy = ANY (ARRAY[1,2]));

update members set banner_privacy = 2 where description_privacy = 2;
update groups set banner_privacy = 2 where description_privacy = 2;
update systems set banner_privacy = 2 where description_privacy = 2;

update info set schema_version = 46;
