-- database version 56
-- premium!

create table premium_subscriptions (
    id serial not null primary key,
    provider text not null,
    provider_id text not null,
    email text not null,
    system_id int references systems(id) on delete set null,
    status text not null,
    next_renewal_at text,
	unique (provider, provider_id)
);

create table premium_allowances (
    subscription_id int not null references premium_subscriptions(id) on delete cascade,
    system_id int references systems(id) on delete set null,
    id_changes_remaining int not null default 0
);

update info set schema_version = 57;
