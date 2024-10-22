create function message_context(account_id bigint, guild_id bigint, channel_id bigint, thread_id bigint)
    returns table (
        system_id int,
        log_channel bigint,
        in_blacklist bool,
        in_log_blacklist bool,
        log_cleanup_enabled bool,
        proxy_enabled bool,
        last_switch int,
        last_switch_members int[],
        last_switch_timestamp timestamp,
        system_tag text,
        system_guild_tag text,
        tag_enabled bool,
        system_avatar text,
        system_guild_avatar text,
        allow_autoproxy bool,
        latch_timeout integer,
        case_sensitive_proxy_tags bool,
        proxy_error_message_enabled bool,
        deny_bot_usage bool
    )
as $$
    -- CTEs to query "static" (accessible only through args) data
    with
        system as (select systems.*, system_config.latch_timeout, system_guild.tag as guild_tag, system_guild.tag_enabled as tag_enabled, 
                          system_guild.avatar_url as guild_avatar,
                          allow_autoproxy as account_autoproxy, system_config.case_sensitive_proxy_tags, system_config.proxy_error_message_enabled from accounts
            left join systems on systems.id = accounts.system
            left join system_config on system_config.system = accounts.system
            left join system_guild on system_guild.system = accounts.system and system_guild.guild = guild_id
            left join abuse_logs on abuse_logs.id = accounts.abuse_log
            where accounts.uid = account_id),
        guild as (select * from servers where id = guild_id)
    select
        system.id                                    as system_id,
        guild.log_channel,
        ((channel_id = any (guild.blacklist))
         or (thread_id = any (guild.blacklist)))     as in_blacklist,
        ((channel_id = any (guild.log_blacklist))
         or (thread_id = any (guild.log_blacklist))) as in_log_blacklist,
        coalesce(guild.log_cleanup_enabled, false),
        coalesce(system_guild.proxy_enabled, true)   as proxy_enabled,
        system_last_switch.switch                    as last_switch,
        system_last_switch.members                   as last_switch_members,
        system_last_switch.timestamp                 as last_switch_timestamp,
        system.tag                                   as system_tag,
        system.guild_tag                             as system_guild_tag,
        coalesce(system.tag_enabled, true)           as tag_enabled,
        system.avatar_url                            as system_avatar,
        system.guild_avatar                          as system_guild_avatar,
        system.account_autoproxy                     as allow_autoproxy,
        system.latch_timeout                         as latch_timeout,
        system.case_sensitive_proxy_tags             as case_sensitive_proxy_tags,
        system.proxy_error_message_enabled           as proxy_error_message_enabled,
        coalesce(abuse_logs.deny_bot_usage, false)   as deny_bot_usage
    -- We need a "from" clause, so we just use some bogus data that's always present
    -- This ensure we always have exactly one row going forward, so we can left join afterwards and still get data
    from (select 1) as _placeholder
        left join system on true
        left join guild on true
        left join accounts on true
        left join system_last_switch on system_last_switch.system = system.id
        left join system_guild on system_guild.system = system.id and system_guild.guild = guild_id
        left join abuse_logs on abuse_logs.id = accounts.abuse_log
$$ language sql stable rows 1;


-- Fetches info about proxying related to a given account/guild
-- Returns one row per member in system, should be used in conjuction with `message_context` too
create function proxy_members(account_id bigint, guild_id bigint)
    returns table (
        id int,
        proxy_tags proxy_tag[],
        keep_proxy bool,
        tts bool,
        server_keep_proxy bool,

        server_name text,
        display_name text,
        name text,

        server_avatar text,
        webhook_avatar text,
        avatar text,

        color char(6),

        allow_autoproxy bool
    )
as $$
    select
        -- Basic data
        members.id                   as id,
        members.proxy_tags           as proxy_tags,
        members.keep_proxy           as keep_proxy,
        members.tts                  as tts,
        member_guild.keep_proxy      as server_keep_proxy,

        -- Name info
        member_guild.display_name    as server_name,
        members.display_name         as display_name,
        members.name                 as name,

        -- Avatar info
        member_guild.avatar_url      as server_avatar,
        members.webhook_avatar_url   as webhook_avatar,
        members.avatar_url           as avatar,

        members.color                as color,

        members.allow_autoproxy      as allow_autoproxy
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

create function generate_hid() returns char(6) as $$
    select string_agg(substr('abcefghjknoprstuvwxyz', ceil(random() * 21)::integer, 1), '') from generate_series(1, 6)
$$ language sql volatile;


create function find_free_system_hid() returns char(6) as $$
declare new_hid char(6);
begin
    loop
        new_hid := generate_hid();
        if not exists (select 1 from systems where hid = new_hid) then return new_hid; end if;
    end loop;
end
$$ language plpgsql volatile;


create function find_free_member_hid() returns char(6) as $$
declare new_hid char(6);
begin
    loop
        new_hid := generate_hid();
        if not exists (select 1 from members where hid = new_hid) then return new_hid; end if;
    end loop;
end
$$ language plpgsql volatile;


create function find_free_group_hid() returns char(6) as $$
declare new_hid char(6);
begin
    loop
        new_hid := generate_hid();
        if not exists (select 1 from groups where hid = new_hid) then return new_hid; end if;
    end loop;
end
$$ language plpgsql volatile;
