interface GroupPrivacy {
    description_privacy?: string | boolean | null;
    icon_privacy?: string | boolean | null;
    list_privacy?: string | boolean | null;
    visibility?: string | boolean | null;
}

export default class Group {
    id?: string;
    uuid?: string;
    name?: string;
    display_name?: string;
    description?: string;
    icon?: string;
    banner?: string;
    color?: string;
    privacy?: GroupPrivacy;

    constructor(data: any) {
        this.id = data.id;
        this.uuid = data.uuid;
        this.name = data.name;
        this.display_name = data.display_name;
        this.description = data.description;
        this.icon = data.icon;
        this.banner = data.banner;
        this.color = data.color;
        if (data.privacy) {
            this.privacy = {}
            this.privacy.description_privacy = data.privacy.description_privacy;
            this.privacy.icon_privacy = data.privacy.icon_privacy;
            this.privacy.list_privacy = data.privacy.list_privacy;
            this.privacy.visibility = data.privacy.visibility;
        }
    }
}