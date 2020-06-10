<template>
    <p class="command-example">
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
    }
    
    .bot-prefix {
        color: $examplePrefixColor;
    }
</style>