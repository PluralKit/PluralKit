-- SCHEMA VERSION 11: 2020-10-23  --
-- Create command message table --

create table command_messages
(
	message_id bigint primary key not null,
	author_id bigint not null
);

update info set schema_version = 11;
