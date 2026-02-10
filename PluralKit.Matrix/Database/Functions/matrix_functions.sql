-- Matrix equivalent of message_context()
-- Uses TEXT params (Matrix IDs) instead of bigint (Discord snowflakes)
-- Joins against matrix_accounts and matrix_rooms instead of accounts and servers
create function matrix_message_context(sender_mxid text, p_room_id text)
    returns table (
        allow_autoproxy bool,

        system_id int,
        system_tag text,
        system_avatar text,

        latch_timeout integer,
        case_sensitive_proxy_tags bool,
        proxy_error_message_enabled bool,
        proxy_switch int,
        name_format text,

        tag_enabled bool,
        proxy_enabled bool,
        system_guild_tag text,
        system_guild_avatar text,
        guild_name_format text,

        last_switch int,
        last_switch_members int[],
        last_switch_timestamp timestamp,

        log_channel bigint,
        in_blacklist bool,
        in_log_blacklist bool,
        log_cleanup_enabled bool,
        require_system_tag bool,
        suppress_notifications bool,

        deny_bot_usage bool
    )
as $$
    select
        -- matrix_accounts table
        matrix_accounts.allow_autoproxy              as allow_autoproxy,

        -- systems table
        systems.id                                   as system_id,
        systems.tag                                  as system_tag,
        systems.avatar_url                           as system_avatar,

        -- system_config table
        system_config.latch_timeout                  as latch_timeout,
        system_config.case_sensitive_proxy_tags       as case_sensitive_proxy_tags,
        system_config.proxy_error_message_enabled     as proxy_error_message_enabled,
        system_config.proxy_switch                   as proxy_switch,
        system_config.name_format                    as name_format,

        -- No guild-specific overrides for Matrix
        true                                         as tag_enabled,
        true                                         as proxy_enabled,
        null::text                                   as system_guild_tag,
        null::text                                   as system_guild_avatar,
        null::text                                   as guild_name_format,

        -- system_last_switch view (shared with Discord)
        system_last_switch.switch                    as last_switch,
        system_last_switch.members                   as last_switch_members,
        system_last_switch.timestamp                 as last_switch_timestamp,

        -- log_channel is null here; Matrix rooms use log_room TEXT in matrix_rooms (not part of MessageContext)
        null::bigint                                 as log_channel,
        coalesce(matrix_rooms.blacklisted, false)    as in_blacklist,
        false                                        as in_log_blacklist,
        false                                        as log_cleanup_enabled,
        false                                        as require_system_tag,
        false                                        as suppress_notifications,

        false                                        as deny_bot_usage

    from (select 1) as _placeholder
        left join matrix_accounts on matrix_accounts.mxid = sender_mxid
        left join systems on systems.id = matrix_accounts.system
        left join system_config on system_config.system = matrix_accounts.system
        left join system_last_switch on system_last_switch.system = matrix_accounts.system
        left join matrix_rooms on matrix_rooms.room_id = p_room_id
$$ language sql stable rows 1;


-- Matrix equivalent of proxy_members(). Takes only sender MXID (TEXT) --
-- no guild parameter since Matrix has no guild-specific member overrides
create function matrix_proxy_members(sender_mxid text)
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
        members.id                   as id,
        members.proxy_tags           as proxy_tags,
        members.keep_proxy           as keep_proxy,
        members.tts                  as tts,
        null::bool                   as server_keep_proxy,

        null::text                   as server_name,
        members.display_name         as display_name,
        members.name                 as name,

        null::text                   as server_avatar,
        members.webhook_avatar_url   as webhook_avatar,
        members.avatar_url           as avatar,

        members.color                as color,

        members.allow_autoproxy      as allow_autoproxy
    from matrix_accounts
        inner join systems on systems.id = matrix_accounts.system
        inner join members on members.system = systems.id
    where matrix_accounts.mxid = sender_mxid
$$ language sql stable rows 10;
