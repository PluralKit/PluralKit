use command_parser::flag::Flag;

pub fn get_list_flags() -> [Flag; 22] {
    [
        // Short or long list
        Flag::from(("full", ["f", "big", "details", "long"])),
        // Search description
        Flag::from((
            "search-description",
            [
                "filter-description",
                "in-description",
                "sd",
                "description",
                "desc",
            ],
        )),
        // Sort properties
        Flag::from(("by-name", ["bn"])),
        Flag::from(("by-display-name", ["bdn"])),
        Flag::from(("by-id", ["bid"])),
        Flag::from(("by-message-count", ["bmc"])),
        Flag::from(("by-created", ["bc", "bcd"])),
        Flag::from((
            "by-last-fronted",
            ["by-last-front", "by-last-switch", "blf", "bls"],
        )),
        Flag::from(("by-last-message", ["blm", "blp"])),
        Flag::from(("by-birthday", ["by-birthdate", "bbd"])),
        Flag::from(("random", ["rand"])),
        // Sort reverse
        Flag::from(("reverse", ["r", "rev"])),
        // Privacy filter
        Flag::from(("all", ["a"])),
        Flag::from(("private-only", ["po"])),
        // Additional fields to include
        Flag::from((
            "with-last-switch",
            ["with-last-fronted", "with-last-front", "wls", "wlf"],
        )),
        Flag::from(("with-last-message", ["with-last-proxy", "wlm", "wlp"])),
        Flag::from(("with-message-count", ["wmc"])),
        Flag::from(("with-created", ["wc"])),
        Flag::from((
            "with-avatar",
            ["with-image", "with-icon", "wa", "wi", "ia", "ii", "img"],
        )),
        Flag::from(("with-pronouns", ["wp", "wprns"])),
        Flag::from(("with-displayname", ["wdn"])),
        Flag::from(("with-birthday", ["wbd", "wb"])),
    ]
}
