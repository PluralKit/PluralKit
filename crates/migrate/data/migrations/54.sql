-- database version 54
-- change suppress notifications server config to an enum

alter table servers 
    alter column suppress_notifications drop default,
    alter column suppress_notifications type int 
        using case when suppress_notifications then 1 else 0 end,
    alter column suppress_notifications set default 0,
    add constraint suppress_notifications_check check (suppress_notifications = ANY (ARRAY[0,1,2,3]));

update info set schema_version = 54;