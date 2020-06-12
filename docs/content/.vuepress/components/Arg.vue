<template>
    <span class="cmd-arg"><slot/></span>
</template>

<style lang="stylus">
    .cmd-arg {
        font-weight: bold;
        line-height: 1.75;

        display: inline-block;
        padding: 0 0.6rem;
        
        background-color: $argBgPlain;
        border-radius: 3px;
    }
        
    .cmd-inline .cmd-arg {
        background-color: $argBgInline;
    }
    
    .cmd-block .cmd-arg {
        background-color: $argBgBlock;
    }
</style>

<script>
    export default {
        props: [],
    }
</script>