module.exports = {
    "system-new": {
        title: "Create a new system",
        summary: "Creates a new system if you do not already have one.",
        usage: [
            {cmd: "system new", desc: "Creates a system with no name."},
            {cmd: "system new `system-name`", desc: "Creates a named system."}
        ],
        arguments: {
            "system-name": {type: "string", desc: "the name of the system to create", optional: true}
        }
    },
    "system-info": {
        title: "Look up info about a system",
        summary: "Shows a system card, either your own or someone else's.",
        usage: [
            {cmd: "system", desc: "Looks up your own system."},
            {cmd: "system `target`", desc: "Looks up another system."}
        ],
        arguments: {
            "target": {type: "system", desc: "the system to look up"}
        }
    }
};