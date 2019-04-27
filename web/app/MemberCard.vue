<template>
  <div class="member-card">
    <div
      class="member-avatar"
      :style="{backgroundImage: `url(${member.avatar_url})`, borderColor: member.color}"
    ></div>
    <div class="member-body">
      <span class="member-name">{{ member.name }}</span>
      <div class="member-description">{{ member.description }}</div>
      <ul class="taglist">
        <li>
          <hash-icon></hash-icon>
          {{ member.id }}
        </li>
        <li v-if="member.birthday">
          <calendar-icon></calendar-icon>
          {{ member.birthday }}
        </li>
        <li v-if="member.pronouns">
          <message-circle-icon></message-circle-icon>
          {{ member.pronouns }}
        </li>
      </ul>
    </div>
  </div>
</template>

<script>
import CalendarIcon from "vue-feather-icons/icons/CalendarIcon";
import HashIcon from "vue-feather-icons/icons/HashIcon";
import MessageCircleIcon from "vue-feather-icons/icons/MessageCircleIcon";

export default {
  props: ["member"],
  components: { HashIcon, CalendarIcon, MessageCircleIcon }
};
</script>

<style lang="scss">
.member-card {
  display: flex;
  flex-direction: row;

  .member-avatar {
    margin: 1.5rem 1rem 0 0;
    border-radius: 50%;
    background-size: cover;
    background-position: top center;
    flex-basis: 4rem;
    height: 4rem;
    border: 4px solid white;
  }

  .member-body {
    flex: 1;
    display: flex;
    flex-direction: column;
    padding: 1rem 1rem 1rem 0;

    .member-name {
      font-size: 13pt;
      font-weight: bold;
    }
  }
}
</style>
