<template>
    <div v-bind:class="{ cmd: true, 'cmd-block': inline === undefined, 'cmd-inline': inline !== undefined }">
        <div class="comment" v-if="comment">// {{ comment }}</div>
        <span class="prefix">pk;</span><slot v-if="usage === undefined"/><span v-if="usage !== undefined">
            <span v-for="part in parse(usage)">
            <span v-if="part.type === 'str'">{{ part.str }}</span>
            <span v-else-if="part.type === 'arg'"><Arg>{{ part.arg }}</Arg></span>
            </span>
        </span>
    </div>
</template>

<style lang="stylus">
    .cmd {
        // Universal command styles (text)
        line-height: 1.7;
        
        color: $cmdColor;
        
        .comment {
            font-weight: bold;
            color: $accentColor;
        }
        
        .prefix {
            font-weight: bold;
            color: $accentColor;
        }
    }
    
    .cmd-block {
        // Base block command styles (also see CmdGroup)
        background-color: $cmdBgBlock;
        border-radius: 6px;
        margin: 0.5rem 0;
        padding: 0.75rem 1.25rem;
    }
        
    .details > .cmd-block {
        margin-top: 1rem;
    }
        
    .cmd-group .cmd-block {
        // Disable most parameters above for grouped commands
        // (they'll get added back by CmdGroup below)
        background-color: transparent;
        padding: 0.1rem 1.25rem;
    }
    
    .cmd-inline {
        display: inline-block;
        
        color: $textColor;
        background-color: $cmdBgInline;
        border-radius: 3px;
        padding: 0 0.7rem;
        margin: 0 0.1rem;
    }
    
    .tip .cmd-inline {
        background-color: $cmdBgInlineTip;
    }
</style>

<script>
    export default {
        props: ["usage", "comment", "inline"],
        methods: {
            parse(usage) {
                const parts = [];
                let lastMatch = 0;

                // matchAll isn't common yet, using exec :(
                const re = /`([^`]+)`/g;
                let match;
                while (match = re.exec(usage)) {
                    if (match.index > 0)
                        parts.push({type: "str", str: usage.substring(lastMatch, match.index)});
                    parts.push({type: "arg", arg: match[1]});
                    lastMatch = match.index + match[0].length;
                }

                if (lastMatch < usage.length)
                    parts.push({type: "str", str: usage.substring(lastMatch)});

                return parts;
            }
        }
    }
</script>