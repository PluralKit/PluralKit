-- database version 41
-- add trusted privacy option
-- add trusted users
-- add privacy_level enum as a domain and change all things referencing privacy to use it
-- change show_private, member_default_private, and group_default_private from booleans to privacy_levels


create table trusted_users
(
    id          serial                          primary key,
    system      serial                          not null references systems (id) on delete cascade,
    uid         bigint                          not null,
    constraint  unique_trusted_user_relation    unique (system, uid)
);
create table trusted_guilds
(
    id          serial                          primary key,
    system      serial                          not null references systems (id) on delete cascade,
    guild       bigint                          not null,
    constraint  unique_trusted_guild_relation   unique (system, guild)
);

create domain privacy_level as integer not null check (value = any (array[1,2,4]));

alter table system_config add default_privacy_shown privacy_level not null default 2;
update system_config set default_privacy_shown = 1 where show_private_info = false;
alter table system_config drop column show_private_info;

alter table system_config add member_default_privacy privacy_level not null default 1;
update system_config set member_default_privacy = 2 where member_default_private = true;
alter table system_config drop column member_default_private;

alter table system_config add group_default_privacy privacy_level not null default 1;
update system_config set group_default_privacy = 2 where group_default_private = true;
alter table system_config drop column group_default_private;


-- screeches in can't modify constraints
alter table systems
    drop constraint systems_description_privacy_check,
    drop constraint systems_front_history_privacy_check,
    drop constraint systems_front_privacy_check,
    drop constraint systems_group_list_privacy_check,
    drop constraint systems_member_list_privacy_check,
    drop constraint systems_pronoun_privacy_check,
    drop constraint systems_name_privacy_check,
    drop constraint systems_avatar_privacy_check,
    alter column description_privacy type privacy_level,
    alter column front_history_privacy type privacy_level,
    alter column front_privacy type privacy_level,
    alter column group_list_privacy type privacy_level,
    alter column member_list_privacy type privacy_level,
    alter column pronoun_privacy type privacy_level,
    alter column name_privacy type privacy_level,
    alter column avatar_privacy type privacy_level
;
alter table members
    drop constraint members_avatar_privacy_check,
    drop constraint members_birthday_privacy_check,
    drop constraint members_description_privacy_check,
    drop constraint members_member_privacy_check,
    drop constraint members_metadata_privacy_check,
    drop constraint members_name_privacy_check,
    drop constraint members_pronoun_privacy_check,
    drop constraint members_proxy_privacy_check,
    alter column avatar_privacy type privacy_level,
    alter column birthday_privacy type privacy_level,
    alter column description_privacy type privacy_level,
    alter column member_visibility type privacy_level,
    alter column metadata_privacy type privacy_level,
    alter column pronoun_privacy type privacy_level,
    alter column name_privacy type privacy_level,
    alter column proxy_privacy type privacy_level
;
alter table groups
    drop constraint groups_description_privacy_check,
    drop constraint groups_icon_privacy_check,
    drop constraint groups_list_privacy_check,
    drop constraint groups_metadata_privacy_check,
    drop constraint groups_name_privacy_check,
    drop constraint groups_visibility_check,
    alter column description_privacy type privacy_level,
    alter column icon_privacy type privacy_level,
    alter column list_privacy type privacy_level,
    alter column metadata_privacy type privacy_level,
    alter column name_privacy type privacy_level,
    alter column visibility type privacy_level
;

update info set schema_version = 41;