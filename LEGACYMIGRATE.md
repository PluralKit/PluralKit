# Legacy bot migration
Until the introduction of the database migration system around December 2019, migrations were done manually.

To bridge the gap between the `legacy` branch's database schema and something the modern migration system can work with, run the following SQL commands on the database:

```sql
-- Create the proxy_tag type
do $$ begin
    create type proxy_tag as (
        prefix text,
        suffix text
    );
exception when duplicate_object then null;
end $$;

-- Add new columns to `members`
alter table members add column IF NOT EXISTS display_name text;
alter table members add column IF NOT EXISTS proxy_tags proxy_tag[] not null default array[]::proxy_tag[];
alter table members add column IF NOT EXISTS keep_proxy bool not null default false;

-- Transfer member proxy tags from the `prefix` and `suffix` columns to the `proxy_tags` array
update members set proxy_tags = array[(members.prefix, members.suffix)]::proxy_tag[]
    where members.prefix is not null or members.suffix is not null;

-- Add other columns
alter table messages add column IF NOT EXISTS original_mid bigint;
alter table servers add column IF NOT EXISTS log_blacklist bigint[] not null default array[]::bigint[];
alter table servers add column IF NOT EXISTS blacklist bigint[] not null default array[]::bigint[];

-- Drop old proxy tag columns
alter table members drop column IF EXISTS prefix cascade;
alter table members drop column IF EXISTS suffix cascade;
```

You should probably take a database backup before doing any of this.

The .NET version of the bot should pick up on any further migrations from this point :) 