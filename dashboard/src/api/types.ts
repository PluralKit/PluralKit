export interface SystemPrivacy {
    description_privacy?: string,
    member_list_privacy?: string,
    front_privacy?: string,
    front_history_privacy?: string,
    group_list_privacy?: string,
    pronoun_privacy?: string,
}

export interface System {
    id?: string;
    uuid?: string;
    name?: string;
    description?: string;
    tag?: string;
    avatar_url?: string;
    banner?: string;
    timezone?: string;
    created?: string;
    privacy?: SystemPrivacy;
    color?: string;
    pronouns?: string;
}

export interface Config {
    timezone: string;
    pings_enabled: boolean;
    member_default_private?: boolean;
    group_default_private?: boolean;
    show_private_info?: boolean;
    member_limit: number;
    group_limit: number;
    description_templates: string[];
}

export interface MemberPrivacy {
    visibility?: string,
    description_privacy?: string,
    name_privacy?: string,
    birthday_privacy?: string,
    pronoun_privacy?: string,
    avatar_privacy?: string,
    metadata_privacy?: string
}

interface proxytag {
    prefix?: string,
    suffix?: string
}

export interface Member {
    id?: string;
    uuid?: string;
    name?: string;
    display_name?: string;
    color?: string;
    birthday?: string;
    pronouns?: string;
    avatar_url?: string;
    webhook_avatar_url?: string;
    banner?: string;
    description?: string;
    created?: string;
    keep_proxy?: boolean;
    tts?: boolean;
    system?: string;
    proxy_tags?: Array<proxytag>;
    privacy?: MemberPrivacy;
}

export interface GroupPrivacy {
    description_privacy?: string,
    icon_privacy?: string,
    list_privacy?: string,
    visibility?: string,
    name_privacy?: string,
    metadata_privacy?: string
}

export interface Group {
    id?: string;
    uuid?: string;
    name?: string;
    display_name?: string;
    description?: string;
    icon?: string;
    banner?: string;
    color?: string;
    privacy?: GroupPrivacy;
    created?: string;
    members?: string[];
}