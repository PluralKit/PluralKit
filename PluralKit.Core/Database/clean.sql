-- This gets run on every bot startup and makes sure we're starting from a clean slate
-- Then, the views/functions.sql files get run, and they recreate the necessary objects
-- This does mean we can't use any functions in row triggers, etc. Still unsure how to handle this.

drop view if exists system_last_switch;
drop view if exists system_fronters;
drop view if exists member_list;
drop view if exists group_list;

drop function if exists autoproxy_context;
drop function if exists message_context;
drop function if exists proxy_members;
drop function if exists has_private_members;
drop function if exists generate_hid;
drop function if exists find_free_system_hid;
drop function if exists find_free_member_hid;
drop function if exists find_free_group_hid;