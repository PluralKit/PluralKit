create table premium_subscriptions (
    id serial primary key,
    provider text not null,
    provider_id text not null,
    email text not null,
    system_id int references systems(id) on delete set null,
    status text,
    next_renewal_at text,
    unique (provider, provider_id)
)