<template>
    <div class="command-info">
        <h3 :id="cmd">
            <a class="header-anchor" :href="'#' + cmd">#</a>
            {{ command.title }}
        </h3>
        <p>{{ command.summary }}</p>

        <slot></slot>

        <CmdGroup>
            <Cmd v-for="usage in command.usage" :comment="usage.desc">
                <span v-for="part in parseUsage(usage)">
                    <span v-if="part.type === 'str'">{{ part.str }}</span>
                    <span v-if="part.type === 'arg'"><Arg>{{ part.arg }}</Arg></span>
                </span>
            </Cmd>
        </CmdGroup>

        <h4>Arguments</h4>
        <div v-for="(arg, key) in command.arguments">
            <Arg>{{ key }}</Arg> (<strong v-if="arg.type === 'string'">text</strong><strong v-if="arg.type === 'system'">system ID</strong><span v-if="arg.optional">, <em>optional</em></span>) - {{ arg.desc }}. 
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

                const parts = usage.split(/\s/);
                const output = [];
                for (const part of parts) {
                    const match = part.match(/`([\w\-]+)`/);
                    if (match)
                        output.push({type: "arg", arg: match[1]});
                    else
                        output.push({type: "str", str: part})
                }
                return output;
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
</style>