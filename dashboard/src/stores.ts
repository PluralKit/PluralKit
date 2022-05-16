import { writable } from 'svelte/store';

export const loggedIn = writable(false);

/* export const user = writable({
    id: null,
    uuid: null,
    name: null,
    description: null,
    tag: null,
    avatar_url: null,
    banner: null,
    timezone: null,
    created: null,
    color: null,
    privacy: {
        description_privacy: null,
        member_list_privacy: null,
        front_privacy: null,
        front_history_privacy: null,
        group_list_privacy: null
    }
}); */

export const currentUser = writable(null);