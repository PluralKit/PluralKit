-- database version 55
-- add premium ID change allowances

alter table system_config
	add column premium_id_changes_remaining int not null default 0,
	add constraint premium_id_changes_nonzero check (premium_id_changes_remaining >= 0);

create table hid_changelog (
	id serial primary key,
	system integer references systems (id) on delete set null,
	discord_uid bigint not null,
	hid_type text not null,
	hid_old char(6) not null,
	hid_new char(6) not null,
	created timestamp not null default (current_timestamp at time zone 'utc')
);

create index hid_changelog_system_idx on hid_changelog (system);

update info set schema_version = 55;