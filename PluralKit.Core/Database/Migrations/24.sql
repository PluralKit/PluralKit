-- schema version 24
-- don't drop message rows when system/member are deleted

alter table messages alter column member drop not null;
alter table messages drop constraint messages_member_fkey;
alter table messages
    add constraint messages_member_fkey
    foreign key (member) references members(id) on delete set null;

update info set schema_version = 24;
