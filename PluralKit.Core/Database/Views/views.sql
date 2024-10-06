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
        end as public_description,
        
        -- Extract member name as seen by "the public"
        case
            -- Privacy '1' = public; just return name as normal
            when members.name_privacy = 1 then members.name
            -- Any other privacy (rn just '2'), return display name
            else coalesce(members.display_name, members.name)
        end as public_name
from members;

create view group_list as
select groups.*,
    -- Find public group member count
    (
        select count(*) from group_members
            inner join members on group_members.member_id = members.id
        where
            group_members.group_id = groups.id and members.member_visibility = 1
    ) as public_member_count,
    -- Find private group member count
    (
        select count(*) from group_members
            inner join members on group_members.member_id = members.id
        where
            group_members.group_id = groups.id
    ) as total_member_count,

    -- Extract group description as seen by "the public"
    case
        -- Privacy '1' = public; just return description as normal
        when groups.description_privacy = 1 then groups.description
        -- Any other privacy (rn just '2'), return null description (missing case = null in SQL)
    end as public_description,
    
    -- Extract member name as seen by "the public"
    case
        -- Privacy '1' = public; just return name as normal
        when groups.name_privacy = 1 then groups.name
        -- Any other privacy (rn just '2'), return display name
        else coalesce(groups.display_name, groups.name)
    end as public_name
from groups;
