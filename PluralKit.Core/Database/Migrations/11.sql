-- SCHEMA VERSION 11: (insert date) --
-- Create command message table --

create table command_message
(
	message_id bigint primary key,
	author_id bigint not null,
	timestamp timestamp not null default now()
);

create function cleanup_command_message() returns void as $$
begin
    delete from command_message where timestamp < now() - interval '1 minute';
end;
$$ language plpgsql;

update info set schema_version = 11;