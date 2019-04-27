import { EventEmitter } from "eventemitter3"

const SITE_ROOT = process.env.NODE_ENV === "production" ? "https://pluralkit.me" : "http://localhost:1234";
const API_ROOT = process.env.NODE_ENV === "production" ? "https://api.pluralkit.me" : "http://localhost:2939";
const CLIENT_ID = process.env.NODE_ENV === "production" ? "466378653216014359" : "467772037541134367";
export const AUTH_URI = `https://discordapp.com/api/oauth2/authorize?client_id=${CLIENT_ID}&redirect_uri=${encodeURIComponent(SITE_ROOT + "/auth/discord")}&response_type=code&scope=identify`


class API extends EventEmitter {
    async init() {
        this.token = localStorage.getItem("pk-token");
        if (this.token) {
            this.me = await fetch(API_ROOT + "/s", {headers: {"X-Token": this.token}}).then(r => r.json());
            this.emit("update", this.me);
        }
    }

    async fetchSystem(id) {
        return await fetch(API_ROOT + "/s/" + id).then(r => r.json()) || null;
    }

    async fetchSystemMembers(id) {
        return await fetch(API_ROOT + "/s/" + id + "/members").then(r => r.json()) || [];
    }

    async fetchSystemSwitches(id) {
        return await fetch(API_ROOT + "/s/" + id + "/switches").then(r => r.json()) || [];
    }

    async fetchMember(id) {
        return await fetch(API_ROOT + "/m/" + id).then(r => r.json()) || null;
    }

    async saveSystem(system) {
        return await fetch(API_ROOT + "/s", {
            method: "PATCH",
            headers: {"X-Token": this.token},
            body: JSON.stringify(system)
        });
    }

    async login(code) {
        this.token = await fetch(API_ROOT + "/discord_oauth", {method: "POST", body: code}).then(r => r.text());
        this.me = await fetch(API_ROOT + "/s", {headers: {"X-Token": this.token}}).then(r => r.json());

        if (this.me) {
            localStorage.setItem("pk-token", this.token);
            this.emit("update", this.me);
        } else {
            this.logout();
        }
        return this.me;
    }

    logout() {
        localStorage.removeItem("pk-token");
        this.emit("update", null);
        this.token = null;
        this.me = null;
    }
}

export default new API();