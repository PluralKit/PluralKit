-- Giant "mega-function" to find all information relevant for message proxying
-- Returns one row per member, computes several properties from others
create function proxy_info(account_id bigint, guild_id bigint)
    returns table
            (
                -- Note: table type gets matched *by index*, not *by name* (make sure order here and in `select` match)
                system_id           int,         -- from: systems.id
                member_id           int,         -- from: members.id
                proxy_tags          proxy_tag[], -- from: members.proxy_tags
                keep_proxy          bool,        -- from: members.keep_proxy
                proxy_enabled       bool,        -- from: system_guild.proxy_enabled
                proxy_name          text,        -- calculated: name we should proxy under
                proxy_avatar        text,        -- calculated: avatar we should proxy with
                autoproxy_mode      int,         -- from: system_guild.autoproxy_mode
                is_autoproxy_member bool,        -- calculated: should this member be used for AP?
                latch_message       bigint,      -- calculated: last message from this account in this guild
                channel_blacklist   bigint[],    -- from: servers.blacklist
                log_blacklist       bigint[],    -- from: servers.log_blacklist
                log_channel         bigint       -- from: servers.log_channel
            )
as
$$
select
    -- Basic data
    systems.id as system_id,
    members.id as member_id,
    members.proxy_tags as proxy_tags,
    members.keep_proxy as keep_proxy,

    -- Proxy info
    coalesce(system_guild.proxy_enabled, true) as proxy_enabled,
    case
        when systems.tag is not null then (coalesce(member_guild.display_name, members.display_name, members.name) || ' ' || systems.tag) 
        else coalesce(member_guild.display_name, members.display_name, members.name)
    end as proxy_name,
    coalesce(member_guild.avatar_url, members.avatar_url, systems.avatar_url) as proxy_avatar,

    -- Autoproxy data
    coalesce(system_guild.autoproxy_mode, 1) as autoproxy_mode,
       
    -- Autoproxy logic is essentially: "is this member the one we should autoproxy?"
    case
        -- Front mode: check if this is the first fronter
        when system_guild.autoproxy_mode = 2 then members.id = (select sls.members[1]
                                                                from system_last_switch as sls
                                                                where sls.system = systems.id)
        
        -- Latch mode: check if this is the last proxier
        when system_guild.autoproxy_mode = 3 then members.id = last_message_in_guild.member
        
        -- Member mode: check if this is the selected memebr
        when system_guild.autoproxy_mode = 4 then members.id = system_guild.autoproxy_member
        
        -- no autoproxy: then this member definitely shouldn't be autoproxied :)
        else false end as is_autoproxy_member,
       
    last_message_in_guild.mid as latch_message,

    -- Guild info
    coalesce(servers.blacklist, array[]::bigint[]) as channel_blacklist,
    coalesce(servers.log_blacklist, array[]::bigint[]) as log_blacklist,
    servers.log_channel as log_channel
from accounts
         -- Fetch guild info
         left join servers on servers.id = guild_id
             
         -- Fetch the system for this account (w/ guild config)
         inner join systems on systems.id = accounts.system
         left join system_guild on system_guild.system = accounts.system and system_guild.guild = guild_id

         -- Fetch all members from this system (w/ guild config)
         inner join members on members.system = systems.id
         left join member_guild on member_guild.member = members.id and member_guild.guild = guild_id

         -- Find ID and member for the last message sent in this guild
         left join lateral (select mid, member
                            from messages
                            where messages.guild = guild_id
                              and messages.sender = account_id
                              and system_guild.autoproxy_mode = 3
                            order by mid desc
                            limit 1) as last_message_in_guild on true
where accounts.uid = account_id;
$$ language sql stable
                rows 10;