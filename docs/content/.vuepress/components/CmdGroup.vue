<template>
    <div class="cmd-group">
        <slot />
    </div>
</template>

<style lang="stylus">
    .cmd-group {
        background-color: $codeBgColor;
        border-radius: 6px;
        margin: 0.5rem 0;
        padding: 0.25rem 0;
    }
    
    .details .cmd-group {
        margin-top: 1rem;
    }
</style>