<template>
  <b-container v-if="loading" class="d-flex justify-content-center">
    <b-spinner class="m-5"></b-spinner>
  </b-container>
  <b-container v-else-if="error">Error</b-container>
  <b-container v-else>
    <h1>Editing "{{member.name}}"</h1>

    <b-form>
      <b-form-group label="Name">
        <b-form-input v-model="member.name" required></b-form-input>
      </b-form-group>

      <b-form-group label="Description">
        <b-form-textarea v-model="member.description" rows="3" max-rows="6"></b-form-textarea>
      </b-form-group>

      <b-form-group label="Proxy tags">
        <b-row>
          <b-col>
            <b-input-group prepend="Prefix">
              <b-form-input class="text-right" v-model="member.prefix" placeholder="ex: ["></b-form-input>
            </b-input-group>
          </b-col>
          <b-col>
            <b-input-group append="Suffix">
              <b-form-input v-model="member.suffix" placeholder="ex: ]"></b-form-input>
            </b-input-group>
          </b-col>
          <b-col></b-col>
        </b-row>
        <template
          v-slot:description
          v-if="member.prefix || member.suffix"
        >Example proxy message: {{member.prefix}}text{{member.suffix}}</template>
        <template v-slot:description v-else>(no prefix or suffix defined, proxying will be disabled)</template>
      </b-form-group>

      <b-form-group label="Pronouns" description="Free text field - put anything you'd like :)">
        <b-form-input v-model="member.pronouns" placeholder="eg. he/him"></b-form-input>
      </b-form-group>

      <b-row>
        <b-col md>
          <b-form-group label="Birthday">
            <b-input-group>
              <b-input-group-prepend is-text>
                <input type="checkbox" v-model="hideBirthday" label="uwu">&nbsp;Hide year
              </b-input-group-prepend>
              <b-form-input v-model="member.birthday" type="date"></b-form-input>
            </b-input-group>
          </b-form-group>
        </b-col>
        <b-col md>
          <b-form-group label="Color" description="Will be displayed on system profile cards.">
            <b-form-input type="color" v-model="member.color"></b-form-input>
          </b-form-group>
        </b-col>
      </b-row>
    </b-form>
  </b-container>
</template>

<script>
import API from "./API";

export default {
  props: ["id"],
  data() {
    return {
      loading: false,
      error: false,
      hideBirthday: false,
      member: null
    };
  },
  created() {
    this.fetch();
  },
  methods: {
    async fetch() {
      this.loading = true;
      this.error = false;
      this.member = await API.fetchMember(this.id);
      if (!this.member) this.error = true;
      this.loading = false;
    }
  }
};
</script>

<style>
</style>
