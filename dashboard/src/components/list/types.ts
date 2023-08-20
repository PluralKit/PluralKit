import type { Group, Member } from '../../api/types';

export interface ListOptions {
    // search and filter based on different things
    // TODO: allow for multiple kinds of searches together
    search: {
        name?: string,
        description?: string,
        id?: string,
        pronouns?: string,
        display_name?: string,
    },

    // true: include only items with this string
    // false: exclude all items with this string
    searchMode: {
        name?: boolean,
        description?: boolean,
        id?: boolean,
        pronouns?: boolean,
        display_name?: boolean,
    }
    // filtering members based on what group they're in (and vice versa)
    groups: {
        // arrays so we can really fine-tune the combinations of groups
        include: {
                exact: boolean, // only include members who are in ALL groups
                list: [],
            },
        exclude: {
                exact: boolean, // only exclude members who are in ALL groups
                list: []
            },
        filter: "all"|"include"|"exclude",
    },
    // filter members based on whether fields have a value set or not
    // if set to true: only include items with a value
    // if set to false: exclude anything with a value
    // if null: don't filter
    filter: {
        description: "all"|"include"|"exclude",
        birthday: "all"|"include"|"exclude",
        pronouns: "all"|"include"|"exclude",
        display_name: "all"|"include"|"exclude",
        avatar_url: "all"|"include"|"exclude",
        icon: "all"|"include"|"exclude",
        color: "all"|"include"|"exclude",
        banner: "all"|"include"|"exclude",

    }
    // filter members based on whether an array field has any items or not
    // used for proxy tags right now
    filterArray: {
        proxy_tags: "all"|"include"|"exclude",
    }
    // what it says on the tin
    sort: 'name'|'description'|'birthday'|'pronouns'|'display_name'|'id'|'none'|'created' | 'color',
    order: "ascending"|"descending",
    show: "all"|"private"|"public",
    
    // text only view options
    extra: keyof Member | keyof Group | null
}

export interface PageOptions {
    // changes availability of certain buttons
    isPublic: boolean, // is this list on the public section?
    isMain: boolean, // is this list on the main dasbhoard, or on another page?
    currentPage: number,
    itemsPerPage: number,
    pageAmount: number,
    view: string,
    randomized: boolean,
    type: 'member'|'group'
}

export interface ShortList {
    name: string,
    shortid: string,
    id: string,
    members?: string[],
    display_name: string,    
}

export interface List<T extends Member|Group> {
    rawList: T[], // the raw list from the API
    processedList: T[], // the list after sorting and filtering
    currentPage: T[], // the slice that represents the current page,

    // for svelecte specifically (member/group selection)
    shortGroups: ShortList[],
    shortMembers: ShortList[],
}

export const defaultListOptions: ListOptions = {
    search: {},
    groups: {
        filter: "all",
        include: {
            exact: false,
            list: []
        },
        exclude: {
            exact: false,
            list: []
        }
    },
    searchMode: {
        name: true,
        display_name: true,
        description: true,
        pronouns: true,
        id: true
    },
    filter: {
        display_name: 'all',
        birthday: 'all',
        description: 'all',
        pronouns: 'all',
        avatar_url: 'all',
        icon: 'all',
        color: 'all',
        banner: 'all'
    },
    filterArray: {
        proxy_tags: 'all',
    },
    sort: 'name',
    order: 'ascending',
    show: 'all',
    extra: 'display_name'
}

export const defaultPageOptions: PageOptions = {
    isPublic: true,
    isMain: true,
    currentPage: 1,
    itemsPerPage: 25,
    pageAmount: 1,
    view: "list",
    randomized: false,
    type: 'member'
}

