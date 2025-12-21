-- database version 55
-- dashboard views

create function generate_dash_view_id_inner() returns char(10) as $$
    select string_agg(substr('aieu234567890', ceil(random() * 13)::integer, 1), '') from generate_series(1, 10)
$$ language sql volatile;


create function generate_dash_view_id() returns char(10) as $$
declare newid char(10);
begin
    loop
        newid := generate_dash_view_id_inner();
        if not exists (select 1 from dash_views where id = newid) then return newid; end if;
    end loop;
end
$$ language plpgsql volatile;

create table dash_views (
    id text not null primary key default generate_dash_view_id(),
    system int references systems(id) on delete cascade,
    name text not null,
    value text not null,
    unique (system, name)
);

update info set schema_version = 55;
