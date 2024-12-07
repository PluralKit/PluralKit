-- database version 44
-- add abuse handling measures

create table abuse_logs (
	id serial primary key,
	uuid uuid default gen_random_uuid(),
	description text,
	deny_bot_usage bool not null default false,
	created timestamp not null default (current_timestamp at time zone 'utc')
);

alter table accounts add column abuse_log integer default null references abuse_logs (id) on delete set null;
create index abuse_logs_uuid_idx on abuse_logs (uuid);

-- we now need to handle a row in "accounts" table being created with no
-- system (rather than just system being set to null after insert)
--
-- set default null and drop the sequence (from the column being created
-- as type SERIAL)
alter table accounts alter column system set default null;
drop sequence accounts_system_seq;

update info set schema_version = 44;
