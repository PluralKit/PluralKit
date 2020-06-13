create view system_last_switch as
select systems.id as system,
       last_switch.id as switch,
       last_switch.timestamp as timestamp,
       array(select member from switch_members where switch_members.switch = last_switch.id) as members
from systems
    inner join lateral (select * from switches where switches.system = systems.id order by timestamp desc limit 1) as last_switch on true;

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
           ) end as birthday_md
from members;