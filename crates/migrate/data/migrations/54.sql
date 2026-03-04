-- database version 54
-- initial support for premium

create table premium_subscriptions (
    id serial primary key,
    provider text not null,
    provider_id text not null,
    email text not null,
    system_id int references systems(id) on delete set null,
    status text,
    next_renewal_at text,
    unique (provider, provider_id)
);

create table premium_allowances (
    subscription_id int primary key references premium_subscriptions(id) on delete cascade,
    system_id int references systems(id) on delete set null,
    -- placeholder
    id_changes_remaining int not null default 0
);

update info set schema_version = 54;
