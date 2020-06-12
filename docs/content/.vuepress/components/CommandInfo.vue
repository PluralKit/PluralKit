<template>
    <div class="command-info">
        <h3 :id="cmd">
            <a class="header-anchor" :href="'#' + cmd">#</a>
            {{ command.title }}
        </h3>
        <p>{{ command.summary }}</p>

        <slot></slot>

        <h4>Syntax <small>(how the command is used)</small></h4>
        <CmdGroup>
            <Cmd v-for="usage in command.usage" :comment="usage.desc">
                <span v-for="part in parseUsage(usage)">
                    <span v-if="part.type === 'str'">{{ part.str }}</span>
                    <span v-if="part.type === 'arg'"><Arg>{{ part.arg }}</Arg></span>
                </span>
            </Cmd>
        </CmdGroup>

        <h4 v-if="command.examples">Examples</h4>
        <CmdGroup v-if="command.examples">
            <Cmd v-for="example in command.examples" :comment="example.desc">
                <span v-for="part in parseUsage(example)">
                    <span v-if="part.type === 'str'">{{ part.str }}</span>
                    <span v-if="part.type === 'arg'"><Arg>{{ part.arg }}</Arg></span>
                </span>
            </Cmd>
        </CmdGroup>

        <h4 v-if="command.arguments">Arguments <small>(fill in above)</small></h4>
        <div class="info-arg" v-for="(arg, key) in command.arguments">
            <Arg>{{ key }}</Arg>
            (<strong v-if="arg.type === 'string'">text</strong><strong v-if="arg.type === 'system'">system
            ID</strong><span v-if="arg.optional">, <em>optional</em></span>) - {{ arg.desc }}.
        </div>

        <h4 v-if="command.flags">Flags <small>(all optional, starts with a hyphen, place anywhere in the
            command)</small></h4>
        <div class="info-flag" v-for="(flag, key) in command.flags">
            <Arg>-{{ key }}</Arg>
            - {{ flag.desc }}
        </div>

        <!--table>
            <thead>
            <tr>
                <th>Argument</th>
                <th>Type</th>
                <th>Description</th>
            </tr>
            </thead>
            <tbody>
            <tr v-for="(arg, key) in command.arguments">
                <td>
                    <Arg>{{ key }}</Arg>
                </td>
                <td>{{ arg.type }}</td>
                <td>{{ arg.desc }}</td>
            </tr>
            </tbody>
        </table-->
    </div>
</template>

<script>
    import commands from "../../../commands/commands";

    export default {
        props: ["cmd"],

        data() {
            return {command: commands[this.cmd]};
        },

        methods: {
            parseUsage(usage) {
                if (usage.cmd) usage = usage.cmd;

                const argRegex = /`([^`]+)`/g;
                const parts = [];
                let lastMatch = 0;
                for (const match of usage.matchAll(argRegex)) {
                    if (match.index > 0)
                        parts.push({type: "str", str: usage.substring(lastMatch, match.index)});
                    parts.push({type: "arg", arg: match[1]});
                    lastMatch = match.index + match[0].length;
                }
                if (lastMatch < usage.length)
                    parts.push({type: "str", str: usage.substring(lastMatch)});
                console.log("INPUT STRING:", usage);
                console.log("OUTPUT TOKENS:", parts);
                return parts;
            }
        }
    }
</script>

<style>
    .command-info {
        margin-bottom: 2.5rem;
    }

    .command-info h3 {
        margin-bottom: 0;
    }

    .command-info p {
        margin-top: 0.25rem;
    }

    .command-info h4 {
        margin-bottom: 0.75rem;
    }

    .info-arg, .info-flag {
        margin-bottom: 0.5rem;
    }
</style>