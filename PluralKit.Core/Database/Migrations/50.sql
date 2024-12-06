-- database version 50
-- change proxy switch config to an enum

alter table system_config 
    alter column proxy_switch drop default,
    alter column proxy_switch type int
        using case when proxy_switch then 1 else 0 end,
    alter column proxy_switch set default 0,
    add constraint proxy_switch_check check (proxy_switch = ANY (ARRAY[0,1,2]));

update info set schema_version = 50;