<template>
    <p class="command-example">
        <span class="example-comment" v-if="comment">// {{ comment }}</span>
        <span class="bot-prefix">pk;</span><slot v-if="usage === undefined"/><span v-if="usage !== undefined">
            <span v-for="part in parse(usage)">
            <span v-if="part.type === 'str'">{{ part.str }}</span>
            <span v-else-if="part.type === 'arg'"><Arg>{{ part.arg }}</Arg></span>
            </span>
        </span>
    </p>
</template>

<style lang="stylus">
    .command-example {
        font-size: $exampleFontSize;
        font-family: $exampleFontFamily;
        color: $exampleTextColor;

        background-color: $exampleBgColor;
        border-radius: 6px;

        /*margin: 1rem 0;*/
        padding: 0.75rem 1.25rem;

        clear: both;

        margin-bottom: 10rem;
    }

    .custom-block.tip .command-example {
        background-color: $exampleBgColorInTip;
    }

    .bot-prefix {
        color: $examplePrefixColor;
    }

    .example-comment {
        color: $exampleCommentColor;
        //float: right;

        //@media (max-width: $MQNarrow) {
        display: block;
        //float: none;
        margin-bottom: -0.15rem;
        //}
    }
</style>

<script>
    export default {
        props: ["usage", "comment"],
        methods: {
            parse(usage) {
                const parts = [];
                let lastMatch = 0;

                for (const match of usage.matchAll(/`([^`]+)`/g)) {
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