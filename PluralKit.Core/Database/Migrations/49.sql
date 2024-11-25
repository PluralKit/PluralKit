-- database version 47
-- remove unused functions

drop function trg_msgcount_decrement cascade;
drop function trg_msgcount_increment cascade;

update info set schema_version = 49;
