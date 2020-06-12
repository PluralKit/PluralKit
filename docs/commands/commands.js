module.exports = {
    systemNew: {
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
    systemInfo: {
        title: "Look up info about a system",
        summary: "Shows a system card, either your own or someone else's.",
        usage: [
            {cmd: "system", desc: "Looks up your own system."},
            {cmd: "system `target`", desc: "Looks up another system."}
        ],
        arguments: {
            "target": {type: "system", desc: "the system to look up"}
        }
    },
    systemName: {
        ...systemSetterCommand("system name", "system name", "new-name", "Boxes of Foxes"),
        title: "Rename your system"
    },
    systemDesc: {
        ...systemSetterCommand("system desc", "system description", "new-description", "Here is my cool new description!", {withRaw: true})
    },

};

function systemSetterCommand(cmdPrefix, valueName, valueArg, exampleVal = "example-value", {argType = "string", withRaw = false} = {}) {
    var args = {};
    args[valueArg] = {type: argType, desc: `the new ${valueName}.`};
    return {
        title: `Change your ${valueName}`,
        summary: `Adds, changes, or removes your ${valueName}.`,
        usage: [
            {cmd: cmdPrefix, desc: `Shows your current ${valueName}.`},
            {cmd: `${cmdPrefix} \`${valueArg}\``, desc: `Sets your ${valueName}.`}
        ],
        examples: [
            {cmd: cmdPrefix, desc: `Shows your current ${valueName}.`},
            {cmd: `${cmdPrefix} -clear`, desc: `Clears your ${valueName}.`},
            {cmd: `${cmdPrefix} \`${exampleVal}\``, desc: `Changes your ${valueName} to '${exampleVal}'`}
        ],
        flags: {
            clear: {desc: `Clear the current ${valueName}.`},
            ...(withRaw ? {raw: {desc: `Show the current ${valueName} in raw form, to copy/paste formatting more easily.`}} : {})
        },
        arguments: args
    }
}