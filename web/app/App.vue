<template>
  <div class="app">
    <b-navbar>
      <b-navbar-brand :to="{name: 'home'}">PluralKit</b-navbar-brand>
      <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>
      <b-collapse id="nav-collapse" is-nav>
        <b-navbar-nav class="ml-auto">
          <b-nav-item v-if="me" :to="{name: 'system', params: {id: me.id}}">My system</b-nav-item>
          <b-nav-item variant="primary" :href="authUri" v-if="!me">Log in</b-nav-item>
          <b-nav-item v-on:click="logout" v-if="me">Log out</b-nav-item>
        </b-navbar-nav>
      </b-collapse>
    </b-navbar>

    <router-view :me="me"></router-view>
  </div>
</template>

<script>
import API from "./API";
import { AUTH_URI } from "./API";

export default {
  data() {
    return {
      me: null
    }
  },
  created() {
    API.on("update", this.apply);
    API.init();
  },
  methods: {
    apply(system) {
      this.me = system;
    },
    logout() {
      API.logout();
    }
  },
  computed: {
    authUri() {
      return AUTH_URI;
    }
  }
};
</script>

<style lang="scss">
$font-family-sans-serif: "PT Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji" !default;
$container-max-widths: (
  sm: 540px,
  md: 720px,
  lg: 959px,
  xl: 960px,
) !default;

@import '~bootstrap/scss/bootstrap.scss';
@import '~bootstrap-vue/src/index.scss';
</style>

