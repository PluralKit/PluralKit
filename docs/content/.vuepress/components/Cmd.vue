<template>
    <p class="command-example">
        <span class="example-comment" v-if="comment">// {{ comment }}</span>
        <span class="bot-prefix">pk;</span><slot></slot>
    </p>
</template>

<style lang="stylus">
    .command-example {
        font-size: $exampleFontSize;
        font-family: $exampleFontFamily;
        color: $exampleTextColor;
        
        background-color: $exampleBgColor;
        border-radius: 6px;
        
        margin: 0.5rem 0;
        padding: 0.75rem 1.25rem;
        
        clear: both;
    }
        
    .custom-block.tip .command-example {
        background-color: darken($exampleBgColor, 3%);
    }
    
    .bot-prefix {
        color: $examplePrefixColor;
    }
    
    .example-comment {
        color: $exampleCommentColor;
        float: right;
    }
</style>

<script>
    export default {
        props: ["comment"]
    }
</script>