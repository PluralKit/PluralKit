<template>
    <b-container>
        <b-container v-if="loading" class="d-flex justify-content-center"><b-spinner class="m-5"></b-spinner></b-container>
        <b-form v-else>
            <h1>Editing "{{ system.name || system.id }}"</h1>
            <b-form-group label="System name">
                <b-form-input v-model="system.name" placeholder="Enter something..."></b-form-input>
            </b-form-group>

            <b-form-group label="Description">
                <b-form-textarea v-model="system.description" placeholder="Enter something..." rows="3" max-rows="3" maxlength="1000"></b-form-textarea>
            </b-form-group>

            <b-form-group label="System tag">
                <b-form-input maxlength="30" v-model="system.tag" placeholder="Enter something..."></b-form-input>
                <template v-slot:description>
                    This is added to the names of proxied accounts. For example: <code>John {{ system.tag }}</code>
                </template>
            </b-form-group>

            <b-form-group class="d-flex justify-content-end">
                <b-button type="reset" variant="outline-secondary">Back</b-button>
                <b-button v-if="!saving" type="submit" variant="primary" v-on:click="save">Save</b-button>
                <b-button v-else variant="primary" disabled>
                    <b-spinner small></b-spinner>
                    <span class="sr-only">Saving...</span>
                </b-button>
            <b-form-group>
        </b-form>
    </b-container>
</template>

<script>
import API from "./API";

export default {
    data() {
        return {
            loading: false,
            saving: false,
            system: null
        }
    },
    props: ["me", "id"],
    created() {
        this.fetch()
    },
    watch: {
        "id": "fetch"
    },
    methods: {
        async fetch() {
            this.loading = true;
            this.system = await API.fetchSystem(this.id);
            if (!this.me || !this.system || this.system.id != this.me.id) {
                this.$router.push({name: "system", params: {id: this.id}});
            }
            this.loading = false;
        },
        async save() {
            this.saving = true;
            if (await API.saveSystem(this.system)) {
                this.$router.push({ name: "system", params: {id: this.system.id} });
            }
            this.saving = false;
        }
    }
}
</script>

<style>

</style>
