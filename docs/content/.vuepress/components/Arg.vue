<template>
    <span class="command-argument"><slot/></span>
</template>

<style lang="stylus">
    .command-argument {
        display: inline-block;
        
        font-size: $exampleFontSize;
        font-family: $exampleFontFamily;
        border-bottom: 1px solid darken($exampleArgColor, 15%);
        color: $exampleTextColor;

        padding: 0 0.5rem;
        background-color: $exampleArgColor;
        border-radius: 4px;

        line-height: 1.7;
    }

    .custom-block.tip .command-argument {
        background-color: $exampleArgColorInTip;
    }

    .command-example .command-argument {
        background-color: $exampleArgColorInExample;
    }

    .custom-block.tip .command-example .command-argument {
        background-color: $exampleArgColorInBoth;
    }
</style>

<script>
    export default {
        props: ["optional", "name", "edit", "value"],
    }
</script>