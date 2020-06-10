<template>
    <span class="example-inline"><span class="inline-bot-prefix">pk;</span><slot /></span>
</template>

<style lang="stylus">
    .example-inline {
        font-size: $exampleFontSize;
        font-family: $exampleFontFamily;

        background-color: $exampleArgColor;
        color: $exampleTextColor;
        padding: 0.2rem 0.4rem;
        border-radius: 3px;
    }
        
    .custom-block.tip .example-inline {
        background-color: darken($exampleArgColor, 3%);
    }

    .inline-bot-prefix {
        color: $examplePrefixColor;
    }
</style>