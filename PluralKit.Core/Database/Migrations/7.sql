-- SCHEMA VERSION 7: 2020-06-12
-- (in-db message count row)

-- Add message count row to members table, initialize it with the correct data
alter table members add column message_count int not null default 0;

update members set message_count = counts.count
from (select member, count(*) as count from messages group by messages.member) as counts
where counts.member = members.id;

-- Create a trigger function to increment the message count on inserting to the messages table
create function trg_msgcount_increment() returns trigger as $$
begin
    update members set message_count = message_count + 1 where id = NEW.member;
    return NEW;
end;
$$ language plpgsql;

create trigger increment_member_message_count before insert on messages for each row execute procedure trg_msgcount_increment();


-- Create a trigger function to decrement the message count on deleting from the messages table
create function trg_msgcount_decrement() returns trigger as $$
begin
    -- Don't decrement if count <= zero (shouldn't happen, but we don't want negative message counts)
    update members set message_count = message_count - 1 where id = OLD.member and message_count > 0;
    return OLD;
end;
$$ language plpgsql;

create trigger decrement_member_message_count before delete on messages for each row execute procedure trg_msgcount_decrement();


-- (update schema ver)
update info set schema_version = 7;