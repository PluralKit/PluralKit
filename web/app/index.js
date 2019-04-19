import Vue from "vue";
import VueRouter from "vue-router";
import BootstrapVue from "bootstrap-vue";
Vue.use(VueRouter);
Vue.use(BootstrapVue);

import App from "./App.vue";
import HomePage from "./HomePage.vue";
import SystemPage from "./SystemPage.vue";
import SystemEditPage from "./SystemEditPage.vue";
import MemberEditPage from "./MemberEditPage.vue";
import OAuthRedirectPage from "./OAuthRedirectPage.vue";

const router = new VueRouter({
    mode: "history",
    routes: [
        { name: "home", path: "/", component: HomePage },
        { name: "system", path: "/s/:id", component: SystemPage, props: true },
        { name: "edit-system", path: "/s/:id/edit", component: SystemEditPage, props: true },
        { name: "edit-member", path: "/m/:id/edit", component: MemberEditPage, props: true },
        { name: "auth-discord", path: "/auth/discord", component: OAuthRedirectPage }
    ]
})
new Vue({ el: "#app", render: r => r(App), router });