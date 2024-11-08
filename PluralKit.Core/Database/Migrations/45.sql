-- database version 45
-- adds banner privacy

alter table members add column banner_privacy int not null default 1;
alter table groups add column banner_privacy int not null default 1;
alter table systems add column banner_privacy int not null default 1;

alter table members add constraint members_banner_privacy_check check (banner_privacy = ANY (ARRAY[1,2]));
alter table groups add constraint groups_banner_privacy_check check (banner_privacy = ANY (ARRAY[1,2]));
alter table systems add constraint systems_banner_privacy_check check (banner_privacy = ANY (ARRAY[1,2]));

update members set banner_privacy = 2 where description_privacy = 2;
update groups set banner_privacy = 2 where description_privacy = 2;
update systems set banner_privacy = 2 where description_privacy = 2;

update info set schema_version = 45;