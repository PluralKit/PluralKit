interface SystemPrivacy {
    description_privacy?: string,
    member_list_privacy?: string,
    front_privacy?: string,
    front_history_privacy?: string,
    group_list_privacy?: string
}

export default class Sys {
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

    constructor(data: any) {
        this.id = data.id;
        this.uuid = data.uuid;
        this.name = data.name;
        this.description = data.description;
        this.tag = data.tag;
        this.avatar_url = data.avatar_url;
        this.banner = data.banner;
        this.timezone = data.timezone;
        this.created = data.created;
        this.color = data.color;
        if (data.privacy) {
            this.privacy = {
                description_privacy: data.privacy.description_privacy,
                member_list_privacy: data.privacy.member_list_privacy,
                front_privacy: data.privacy.front_privacy,
                front_history_privacy: data.privacy.front_history_privacy,
                group_list_privacy: data.privacy.group_list_privacy
            }
        }
    }
}