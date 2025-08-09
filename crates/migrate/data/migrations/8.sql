-- SCHEMA VERSION 8: 2020-05-13 --
-- Create new columns --
alter table members add column description_privacy integer check (description_privacy in (1, 2)) not null default 1;
alter table members add column name_privacy integer check (name_privacy in (1, 2)) not null default 1;
alter table members add column avatar_privacy integer check (avatar_privacy in (1, 2)) not null default 1;
alter table members add column birthday_privacy integer check (birthday_privacy in (1, 2)) not null default 1;
alter table members add column pronoun_privacy integer check (pronoun_privacy in (1, 2)) not null default 1;
alter table members add column metadata_privacy integer check (metadata_privacy in (1, 2)) not null default 1;
-- alter table members add column color_privacy integer check (color_privacy in (1, 2)) not null default 1;

-- Transfer existing settings --
update members set description_privacy = member_privacy;
update members set name_privacy = member_privacy;
update members set avatar_privacy = member_privacy;
update members set birthday_privacy = member_privacy;
update members set pronoun_privacy = member_privacy;
update members set metadata_privacy = member_privacy;
-- update members set color_privacy = member_privacy;

-- Rename member_privacy to member_visibility --
alter table members rename column member_privacy to member_visibility;

-- Update Schema Info --
update info set schema_version = 8;
