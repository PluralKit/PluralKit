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
    system_uuid uuid not null,
    image text references images_hashes(hash),
    proxy_image text references images_hashes(hash),
    kind text not null,

    original_url text,
    original_file_size int,
    original_type text,
    original_attachment_id bigint,

    uploaded_by_account bigint,
    uploaded_by_ip inet,
    uploaded_at timestamptz not null default now()

    unique (id, system_uuid)
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

alter table images rename to images_legacy;

create index if not exists images_original_url_idx on images_assets (original_url);
create index if not exists images_original_attachment_id_idx on images_assets (original_attachment_id);
create index if not exists images_uploaded_by_account_idx on images_assets (uploaded_by_account);

create index if not exists images_system_id_idx on images_assets (system_uuid);
create index if not exists images_proxy_hash_idx on images_assets (image);

-- image cleanup stuffs
alter table image_cleanup_jobs rename to image_cleanup_jobs_legacy;

create table image_cleanup_jobs (
    id uuid primary key,
    system_uuid uuid not null,
    
    foreign key (id, system_uuid) 
        references images_assets(id, system_uuid) 
        on delete cascade
);

alter table image_cleanup_pending_jobs rename to image_cleanup_pending_jobs_legacy;

create table image_cleanup_pending_jobs (
    id uuid primary key,
    system_uuid uuid not null,
    ts timestamp not null default now(),

    foreign key (id, system_uuid)
        references images_assets(id, system_uuid)
        on delete cascade
);

create table image_hash_cleanup_jobs (
    hash text primary key

    foreign key (hash)
        references images_hashes(hash)
        on delete cascade
);

create table image_hash_cleanup_pending_jobs (
    hash text primary key,
    ts timestamp not null default now()

    foreign key (hash)
        references images_hashes(hash)
        on delete cascade
);