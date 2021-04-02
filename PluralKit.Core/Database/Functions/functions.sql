create function autoproxy_context(system_id int, guild_id bigint, channel_id bigint)
    returns table (
        scope int,
        mode int,
        location bigint,
        member int
    )
as $$
    with
        channel as (select * from autoproxy where autoproxy.system = system_id and autoproxy.location = channel_id and autoproxy.scope = 3),
        guild as (select * from autoproxy where autoproxy.system = system_id and autoproxy.location = guild_id and autoproxy.scope = 2),
        global as (select * from autoproxy where autoproxy.system = system_id and autoproxy.location = 0 and autoproxy.scope = 1)
    select
        coalesce(channel.scope, guild.scope, global.scope) as scope,
        coalesce(channel.mode, guild.mode, global.mode, 1) as mode,
        coalesce(channel.location, guild.location, global.location) as location,
        coalesce(channel.member, guild.member, global.member) as member
    from (select 1) as _placeholder
        left join channel on true
        left join guild on true
        left join global on true
$$ language sql stable rows 1;


create function message_context(account_id bigint, guild_id bigint, channel_id bigint)
    returns table (
        system_id int,
        log_channel bigint,
        in_blacklist bool,
        in_log_blacklist bool,
        log_cleanup_enabled bool,
        proxy_enabled bool,
        autoproxy_mode int,
        autoproxy_scope int,
        autoproxy_member int,
        autoproxy_location bigint,
        last_message bigint,
        last_message_member int,
        last_switch int,
        last_switch_members int[],
        last_switch_timestamp timestamp,
        system_tag text,
        system_avatar text,
        allow_autoproxy bool,
        latch_timeout integer
    )
as $$
    -- CTEs to query "static" (accessible only through args) data
    with
        system as (select systems.*, allow_autoproxy as account_autoproxy from accounts inner join systems on systems.id = accounts.system where accounts.uid = account_id),
        guild as (select * from servers where id = guild_id),
        -- TODO: how do we get the correct last_message for the autoproxy scope we're in?
        -- (do we even need to?)
        last_message as (select * from messages where messages.guild = guild_id and messages.sender = account_id order by mid desc limit 1)
    select
        system.id as system_id,
        guild.log_channel,
        (channel_id = any(guild.blacklist)) as in_blacklist,
        (channel_id = any(guild.log_blacklist)) as in_log_blacklist,
        coalesce(guild.log_cleanup_enabled, false),
        coalesce(system_guild.proxy_enabled, true) as proxy_enabled,

        -- TODO: *don't*
        (select mode from autoproxy_context(system.id, guild_id, channel_id)) as autoproxy_mode,
        (select scope from autoproxy_context(system.id, guild_id, channel_id)) as autoproxy_scope,
        (select member from autoproxy_context(system.id, guild_id, channel_id)) as autoproxy_member,
        (select location from autoproxy_context(system.id, guild_id, channel_id)) as autoproxy_location,

        last_message.mid as last_message,
        last_message.member as last_message_member,

        system_last_switch.switch as last_switch,
        system_last_switch.members as last_switch_members,
        system_last_switch.timestamp as last_switch_timestamp,
        system.tag as system_tag,
        system.avatar_url as system_avatar,
        system.account_autoproxy as allow_autoproxy,
        system.latch_timeout as latch_timeout
    -- We need a "from" clause, so we just use some bogus data that's always present
    -- This ensure we always have exactly one row going forward, so we can left join afterwards and still get data
    from (select 1) as _placeholder
        left join system on true
        left join guild on true
        left join last_message on true
        left join system_last_switch on system_last_switch.system = system.id
        left join system_guild on system_guild.system = system.id and system_guild.guild = guild_id
$$ language sql stable rows 1;


-- Fetches info about proxying related to a given account/guild
-- Returns one row per member in system, should be used in conjuction with `message_context` too
create function proxy_members(account_id bigint, guild_id bigint)
    returns table (
        id int,
        proxy_tags proxy_tag[],
        keep_proxy bool,
        
        server_name text,
        display_name text,
        name text,
        
        server_avatar text,
        avatar text,

        allow_autoproxy bool
    )
as $$
    select
        -- Basic data
        members.id as id,
        members.proxy_tags as proxy_tags,
        members.keep_proxy as keep_proxy,
    
        -- Name info
        member_guild.display_name as server_name,
        members.display_name as display_name,
        members.name as name,
        
        -- Avatar info
        member_guild.avatar_url as server_avatar,
        members.avatar_url as avatar,

        members.allow_autoproxy as allow_autoproxy
    from accounts
        inner join systems on systems.id = accounts.system
        inner join members on members.system = systems.id
        left join member_guild on member_guild.member = members.id and member_guild.guild = guild_id
    where accounts.uid = account_id
$$ language sql stable rows 10;

create function has_private_members(system_hid int) returns bool as $$
declare m int;
begin
    m := count(id) from members where system = system_hid and member_visibility = 2;
    if m > 0 then return true;
    else return false;
    end if;
end
$$ language plpgsql;

create function generate_hid() returns char(5) as $$
    select string_agg(substr('abcdefghijklmnopqrstuvwxyz', ceil(random() * 26)::integer, 1), '') from generate_series(1, 5)
$$ language sql volatile;


create function find_free_system_hid() returns char(5) as $$
declare new_hid char(5);
begin
    loop
        new_hid := generate_hid();
        if not exists (select 1 from systems where hid = new_hid) then return new_hid; end if;
    end loop;
end
$$ language plpgsql volatile;


create function find_free_member_hid() returns char(5) as $$
declare new_hid char(5);
begin
    loop
        new_hid := generate_hid();
        if not exists (select 1 from members where hid = new_hid) then return new_hid; end if;
    end loop;
end
$$ language plpgsql volatile;

create function find_free_group_hid() returns char(5) as $$
declare new_hid char(5);
begin
    loop
        new_hid := generate_hid();
        if not exists (select 1 from groups where hid = new_hid) then return new_hid; end if;
    end loop;
end
$$ language plpgsql volatile;