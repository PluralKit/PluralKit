-- database version 56
-- add premium allowances / hid changelog

create table premium_allowances (
	id serial primary key,
	system integer references systems (id) on delete set null,
	id_changes_remaining int not null default 0 check (id_changes_remaining >= 0),
	unique (system)
);

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

update info set schema_version = 56;