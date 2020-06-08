-- SCHEMA VERSION 9
-- Set up new tables
alter table systems add column tag_prefix text;
alter table systems rename column tag to tag_suffix;
-- Prepare suffix for new format
update systems set tag_suffix = concat(' ',tag_suffix);
-- Update schema
update info set schema_version = 9;