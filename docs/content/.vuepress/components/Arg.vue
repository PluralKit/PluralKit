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
        
    // "Free" arguments in a standard text section needs extra margin
    // might need tweaking to look good in all contexts, but this looks fine for now
    p > .cmd-arg, li > .cmd-arg {
        margin: 0.2rem 0.2rem 0.2rem 0.1rem;
    }
</style>

<script>
    export default {
        props: [],
    }
</script>