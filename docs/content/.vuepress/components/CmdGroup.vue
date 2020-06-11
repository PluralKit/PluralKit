<template>
    <div class="command-example-group">
        <slot />
    </div>
</template>

<style lang="stylus">
    .command-example-group {
        background-color: $exampleBgColor;
        border-radius: 6px;

        margin: 0.5rem 0;
        padding: 0.75rem 1.25rem;
        
        clear: both;
    }
        
    .custom-block.tip .command-example-group {
        background-color: $exampleBgColorInTip;
    }
    
    .command-example-group .command-example {
        margin: 0;
        padding: 0;
    }
    
    .command-example-group .command-example:not(:last-child) {
        margin-bottom: 0.5rem;
    }
</style>