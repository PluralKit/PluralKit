const fs = require('fs');
let commandReference;

try {
    commandReference = require('./command_reference.json');
} catch(e) {
    console.error('\x1b[31m%s\x1b[0m', "The command reference JSON file is invalid or inaccessible.");
    process.exit(1);
}

const commands = {};
commandReference.commands.forEach(cmd => commands[cmd.key] = cmd);

const header = `---
layout: default
title: Command List
permalink: /commands
description: The full list of all commands in PluralKit, and a short description of what they do.
nav_order: 3
---

# How to read this
Words in **\\<angle brackets>** or **[square brackets]** mean fill-in-the-blank. Square brackets mean this is optional. Don't include the actual brackets.

# Commands`;

switch(process.argv[2])
{
    case "--build": 
    {

        let output = [ header ];

        for (let grp of commandReference.groups) {
            if (!grp.title) continue;
            let ret = "";
            ret += `## ${grp.title}\n`;
            if (grp.description) ret += grp.description + "\n";

            for (let cmdString of grp.commands) {
                let cmd = commands[cmdString];
                ret += `- \`pk;${cmd.usage}\` - ${cmd.description.replace("https://pluralkit.me", ".")}.\n`;
            }

            output.push(ret);
        }

        fs.writeFileSync("./content/command-list.md", output.join("\n"));

        break;
    }
    case "--check":
    {

        let found = [];
        let extra = [];

        for (let grp of commandReference.groups) {
            if (!grp.title) continue;
            grp.commands.forEach(cmd => {
                if (found.includes(cmd)) extra.push(cmd);
                else {
                    found.push(cmd);
                    delete commands[cmd];
                };
            })
        }

        if (extra.length > 0) {
            console.warn('\x1b[31m%s\x1b[0m', "The following commands were found in multiple groups shown in the docs:") 
            console.warn(extra.map(cmd => {
                let grps = [];
                for (let grp of commandReference.groups) if (grp.commands.includes(cmd)) grps.push(grp.key);
                return `${cmd} (in ${grps.join(", ")})`;
            }).join("\n"));
        }

        if (Object.keys(commands).length > 0) {
            console.warn('\x1b[31m%s\x1b[0m', "The following commands were not found to be listed on the docs:")
            console.warn(Object.keys(commands).join("\n"));
        }

        if (extra.length > 0 || Object.keys(commands).length > 0) process.exit(1);
        else console.log('\x1b[32m%s\x1b[0m', "The command reference file is valid.");

        break;
    }
    default:
        if (!process.argv[2]) console.error("Missing one of `--build`, `--check`");
        else console.error(`Unknown argument \`${process.argv[2]}\``);
}