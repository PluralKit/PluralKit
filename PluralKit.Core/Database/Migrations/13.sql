-- SCHEMA VERSION 12: 2020-11-02 --
-- Add notes field for switches

alter table switches add column note text;

update info set schema_version = 13;