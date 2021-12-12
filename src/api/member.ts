interface MemberPrivacy {
    visibility?: string | boolean | null,
    description_privacy?: string | boolean | null,
    name_privacy?: string | boolean | null,
    birthday_privacy?: string | boolean | null,
    pronoun_privacy?: string | boolean | null,
    avatar_privacy?: string | boolean | null,
    metadata_privacy?: string | boolean | null
}

export default class Member {
    id?: string;
    uuid?: string;
    name?: string;
    display_name?: string;
    color?: string;
    birthday?: string;
    pronouns?: string;
    avatar_url?: string;
    banner?: string;
    description?: string;
    created?: string;
    keep_proxy?: boolean
    system?: string;
    proxy_tags?: Array<object>;
    privacy?: MemberPrivacy

    constructor(data: any) {
        this.id = data.id;
        this.uuid = data.uuid;
        this.name = data.name;
        this.display_name = data.display_name;
        this.color = data.color;
        this.birthday = data.birthday;
        this.pronouns = data.pronouns;
        this.avatar_url = data.avatar_url;
        this.banner = data.banner;
        this.description = data.description;
        this.created = data.created;
        this.system = data.system;
        this.proxy_tags = data.proxy_tags;
        this.keep_proxy = data.keep_proxy;
        if (data.privacy) {
            this.privacy = {
                visibility: data.privacy.visibility,
                description_privacy: data.privacy.description_privacy,
                name_privacy: data.privacy.name_privacy,
                birthday_privacy: data.privacy.birthday_privacy,
                pronoun_privacy: data.privacy.pronouns_privacy,
                avatar_privacy: data.privacy.avatar_privacy,
                metadata_privacy: data.privacy.metadata_privacy
            }
        }
    }
}