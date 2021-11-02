-- schema version 20: insert date
-- add outgoing webhook to systems

alter table systems add column webhook_url text;
alter table systems add column webhook_token text;

update info set schema_version = 20;