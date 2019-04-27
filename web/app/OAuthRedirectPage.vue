<template>
    <b-container class="d-flex justify-content-center"><span class="sr-only">Loading...</span><b-spinner class="m-5"></b-spinner></b-container>
</template>

<script>
import API from "./API";

export default {
    async created() {
        const code = this.$route.query.code;
        if (!code) this.$router.push({ name: "home" });
        const me = await API.login(code);
        if (me) this.$router.push({ name: "home" });
    }
}
</script>

<style>

</style>
