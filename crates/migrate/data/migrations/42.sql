-- database version 42
-- move to 6 character HIDs, add HID display config setting

alter table systems alter column hid type char(6) using rpad(hid, 6, ' ');
alter table members alter column hid type char(6) using rpad(hid, 6, ' ');
alter table groups alter column hid type char(6) using rpad(hid, 6, ' ');

alter table system_config add column hid_display_split bool default false;
alter table system_config add column hid_display_caps bool default false;

update info set schema_version = 42;