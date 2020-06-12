drop view if exists system_last_switch;
drop view if exists member_list;

drop function if exists message_context;
drop function if exists proxy_members;
drop function if exists generate_hid;
drop function if exists find_free_system_hid;
drop function if exists find_free_member_hid;