import Vue from "vue";

import VueRouter from "vue-router";
Vue.use(VueRouter);

const App = () => import("./App.vue");
const HomePage = () => import("./HomePage.vue");
const SystemPage = () => import("./SystemPage.vue");
const SystemEditPage = () => import("./SystemEditPage.vue");
const MemberEditPage = () => import("./MemberEditPage.vue");
const OAuthRedirectPage = () => import("./OAuthRedirectPage.vue");

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