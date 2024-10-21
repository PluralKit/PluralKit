create table if not exists images
(
    id                     text primary key,
    url                    text        not null,
    original_url           text,
    original_file_size     int,
    original_type          text,
    original_attachment_id bigint,
    file_size              int         not null,
    width                  int         not null,
    height                 int         not null,
    kind                   text        not null,
    uploaded_at            timestamptz not null,
    uploaded_by_account    bigint
);

create index if not exists images_original_url_idx on images (original_url);
create index if not exists images_original_attachment_id_idx on images (original_attachment_id);
create index if not exists images_uploaded_by_account_idx on images (uploaded_by_account);

create table if not exists image_queue (itemid serial primary key, url text not null, kind text not null);

alter table images add column if not exists uploaded_by_system uuid;
alter table images add column if not exists content_type text default 'image/webp';