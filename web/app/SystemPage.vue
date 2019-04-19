<template>
  <b-container v-if="loading" class="d-flex justify-content-center"><b-spinner class="m-5"></b-spinner></b-container>
  <b-container v-else-if="error">An error occurred.</b-container>
  <b-container v-else>
    <ul v-if="system" class="taglist">
      <li>
        <hash-icon></hash-icon>
        {{ system.id }}
      </li>
      <li v-if="system.tag">
        <tag-icon></tag-icon>
        {{ system.tag }}
      </li>
      <li v-if="system.tz">
        <clock-icon></clock-icon>
        {{ system.tz }}
      </li>
      <li v-if="isMine" class="ml-auto">
        <b-link :to="{name: 'edit-system', params: {id: system.id}}">
          <edit-2-icon></edit-2-icon>
          Edit
        </b-link>
      </li>
    </ul>

    <h1 v-if="system && system.name">{{ system.name }}</h1>
    <div v-if="system && system.description">{{ system.description }}</div>

    <h2>Members</h2>
    <div v-if="members">
      <MemberCard v-for="member in members" :member="member" :key="member.id"/>
    </div>
  </b-container>
</template>

<script>
import API from "./API";

import MemberCard from "./MemberCard.vue";

import { Edit2Icon, ClockIcon, HashIcon, TagIcon } from "vue-feather-icons";

export default {
  data() {
    return {
      loading: false,
      error: false,
      system: null,
      members: null
    };
  },
  props: ["me", "id"],
  created() {
    this.fetch();
  },
  methods: {
    async fetch() {
      this.loading = true;
      this.system = await API.fetchSystem(this.id);
      if (!this.system) {
        this.error = true;
        this.loading = false;
        return;
      }
      this.members = await API.fetchSystemMembers(this.id);
      this.loading = false;
    }
  },
  watch: {
    id: "fetch"
  },
  computed: {
    isMine() {
      return this.system && this.me && this.me.id == this.system.id;
    }
  },
  components: {
    Edit2Icon,
    ClockIcon,
    HashIcon,
    TagIcon,
    MemberCard
  }
};
</script>

<style lang="scss">
.taglist {
  margin: 0;
  padding: 0;
  color: #aaa;
  display: flex;

  li {
    display: inline-block;
    margin-right: 1rem;
    list-style-type: none;
    .feather {
      display: inline-block;
      margin-top: -2px;
      width: 1em;
    }
  }
}
</style>
