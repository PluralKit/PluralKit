-- Returns one row per system, containing info about latest switch + array of member IDs (for future joins)
create view system_last_switch as
select systems.id     as system,
       last_switch.id as switch,
       last_switch.timestamp as timestamp,
       array(select member from switch_members where switch_members.switch = last_switch.id order by switch_members.id) as members
from systems
    inner join lateral (select * from switches where switches.system = systems.id order by timestamp desc limit 1) as last_switch on true;

create view member_list as
select members.*,
       -- Find last switch timestamp
       (
           select max(switches.timestamp)
           from switch_members
                    inner join switches on switches.id = switch_members.switch
           where switch_members.member = members.id
       ) as last_switch_time,

        -- Aggregate list of group ids attached to each member
       (
           select array_agg(groups.id)
            from group_members
                left join groups on groups.id = group_members.group_id
            where group_members.member_id = members.id
       ) as group_ids
from members;

create view group_list as
select groups.*,
    -- Aggregate list of group ids attached to each member
    (
        select array_agg(members.id)
        from group_members
            left join members on members.id = group_members.member_id
        where group_members.group_id = groups.id
    ) as member_ids
from groups;
