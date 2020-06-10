<template>
    <div class="command-argument-parent">
        <div class="command-argument">
            <slot />
        </div>
        
        <div v-if="false" class="argument-details">
            {{ name }}
            <span v-if="optional !== undefined">(optional)</span>
        </div>
    </div>

</template>

<style lang="stylus">
    .command-argument-parent {
        display: inline-flex;
        flex-direction: column;

        text-align: center;
    }
        
    .command-argument {
        font-size: $exampleFontSize;
        font-family: $exampleFontFamily;
        color: $exampleTextColor;

        padding: 0 0.5rem;
        background-color: lighten($exampleBgColor, 15%);
        border-radius: 4px;
        
        line-height: 1.7;
    }
    
    .argument-details {
        font-size: 0.75em;
        font-style: italic; 
    }
    
    .arg-input {
        /*font-size: 0.9rem;*/
        color: $exampleTextColor;
        background-color: transparent;
        text-align: center;
        border: none;
    }
</style>

<script>
    export default {
        props: ["optional", "name", "edit", "value"],
    }
</script>