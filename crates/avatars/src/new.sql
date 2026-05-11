create table images_hashes (
    hash text primary key,
    url text not null,
    file_size int not null,
    width int not null,
    height int not null,
    content_type text not null,
    created_at timestamptz not null default now()
);

create table images_assets (
    id uuid primary key default gen_random_uuid(),
    system_id int not null references systems(id) on delete cascade,
    image text not null,
    proxy_image text,
    kind text not null,

    original_url text,
    original_file_size int,
    original_type text,
    original_attachment_id bigint,

    uploaded_by_account bigint,
    uploaded_by_ip inet,
    uploaded_at timestamptz not null default now(),
    deleted_at timestamptz,

    unique (id, system_id)
);

insert into images_hashes (
    hash, 
    url, 
    file_size, 
    width, 
    height, 
    content_type, 
    created_at
)
select 
    id,
    url, 
    file_size, 
    width, 
    height, 
    coalesce(content_type, 'image/webp'),
    uploaded_at
from images;
-- todo(premium): migrate to images_assets

alter table images rename to images_legacy;

create index images_original_url_idx on images_assets (original_url);
create index images_original_attachment_id_idx on images_assets (original_attachment_id);
create index images_uploaded_by_account_idx on images_assets (uploaded_by_account);

create index images_system_id_idx on images_assets (system_id);
create index images_proxy_hash_idx on images_assets (image);

-- image cleanup stuffs
drop table image_cleanup_jobs;
drop table image_cleanup_pending_jobs;

create table image_cleanup_jobs (
    id uuid primary key,
    system_id int not null,
    ts timestamp not null default now(),

    foreign key (id, system_id)
        references images_assets(id, system_id)
        on delete cascade
);

create table image_hash_cleanup_jobs (
    hash text primary key,
    ts timestamp not null default now(),

    foreign key (hash)
        references images_hashes(hash)
        on delete cascade
);
