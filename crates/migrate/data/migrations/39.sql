-- database version 39
-- add missing privacy constraints

alter table systems add constraint systems_name_privacy_check check (name_privacy = ANY (ARRAY[1,2]));
alter table systems add constraint systems_avatar_privacy_check check (avatar_privacy = ANY (ARRAY[1,2]));

update info set schema_version = 39;