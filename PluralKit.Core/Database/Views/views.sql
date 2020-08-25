-- Returns one row per system, containing info about latest switch + array of member IDs (for future joins)
create view system_last_switch as
select systems.id as system,
       last_switch.id as switch,
       last_switch.timestamp as timestamp,
       array(select member from switch_members where switch_members.switch = last_switch.id) as members
from systems
    inner join lateral (select * from switches where switches.system = systems.id order by timestamp desc limit 1) as last_switch on true;

-- Returns one row for every current fronter in a system, w/ some member info
create view system_fronters as
select
    systems.id as system_id,
    last_switch.id as switch_id,
    last_switch.timestamp as switch_timestamp,
    members.id as member_id,
    members.hid as member_hid,
    members.name as member_name
from systems
    -- TODO: is there a more efficient way of doing this search? might need to index on timestamp if we haven't in prod
    inner join lateral (select * from switches where switches.system = systems.id order by timestamp desc limit 1) as last_switch on true
        
    -- change to left join to handle memberless switches?
    inner join switch_members on switch_members.switch = last_switch.system
    inner join members on members.id = switch_members.member
-- return them in order of the switch itself
order by switch_members.id;

create view member_list as
select members.*,
       -- Find last message ID
       -- max(mid) does full table scan, order by/limit uses index (dunno why, but it works!)
       (select mid from messages where messages.member = members.id order by mid desc nulls last limit 1) as last_message,
       
       -- Find last switch timestamp
       (
           select max(switches.timestamp)
           from switch_members
                    inner join switches on switches.id = switch_members.switch
           where switch_members.member = members.id
       ) as last_switch_time,
       
       -- Extract month/day from birthday and "force" the year identical (just using 4) -> month/day only sorting! 
       case when members.birthday is not null then
           make_date(
               4,
               extract(month from members.birthday)::integer,
               extract(day from members.birthday)::integer
           ) end as birthday_md,

        -- Extract member description as seen by "the public"
        case 
            -- Privacy '1' = public; just return description as normal
            when members.description_privacy = 1 then members.description
            -- Any other privacy (rn just '2'), return null description (missing case = null in SQL)
        end as public_description
from members;

create view group_list as
select groups.*,
    -- Find public group member count
    (
        select count(*) from group_members 
            inner join members on group_members.member_id = members.id 
        where 
            group_members.group_id = groups.id and members.member_visibility = 1
    ) as member_count
from groups;