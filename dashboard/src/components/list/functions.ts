import type { Group, Member } from '../../api/types';
import type { ListOptions, PageOptions } from './types';

export function filterList(list: Group[]|Member[], groups: Group[], options: ListOptions, type?: string): Group[]|Member[] {
    let searchedList = search(list, options);
    
    let groupedList = [...searchedList];
    if (type)
        groupedList = group(searchedList, groups, options, type);
    let filteredList = filter(groupedList, options);
    let sortedList = sort(filteredList, options);
    let orderedList = reorder(sortedList, options);

    return orderedList;
}

export function paginateList<T extends Member|Group>(list: T[], options: PageOptions): T[] {
    let indexLast = options.currentPage * options.itemsPerPage;
    let indexFirst = indexLast - options.itemsPerPage;
    return list.slice(indexFirst, indexLast);
}

export function getPageAmount<T extends Member|Group>(list: T[], options: PageOptions): number {
    return Math.ceil(list.length / options.itemsPerPage);
}

export function createShortList<T extends Member|Group>(list: T[]) {
    return list.map(function(item) { 
        return {
            name: item.name, 
            shortid: item.id, 
            id: item.uuid, 
            members: (item as Group).members, 
            display_name: item.display_name}; 
        })
        .sort((a, b) => a.name.localeCompare(b.name));
}

function search<T extends Member|Group>(list: T[], options: ListOptions): T[] {
    Object.keys(options.search).forEach(x => {
        if (options.search[x]) {
            if (options.searchMode[x] !== false)
                list = list.filter(item => item[x] ? item[x].toLowerCase().includes(options.search[x].toLowerCase()) : false);
            else list = list.filter(item => item[x] ? !item[x].toLowerCase().includes(options.search[x].toLowerCase()) : true);
        }
    });

    return list;
}

function filter<T extends Member|Group>(list: T[], options: ListOptions): T[] {
    let newList = [...list];
    
    Object.keys(options.filter).forEach(x => {
        if (options.filter[x] === 'include') {
            newList = [...newList].filter(item => item[x] && true);
        }
    });

    Object.keys(options.filter).forEach(x => {
        if (options.filter[x] === 'exclude') {
            newList = [...newList].filter(item => !item[x] && true)
        }
    });

    Object.keys(options.filterArray).forEach(x => {
        if (options.filterArray[x] === 'include') {
            newList = [...newList].filter(i => i[x] && i[x].length > 0 ? true : false)
        }
    })

    Object.keys(options.filterArray).forEach(x => {
        if (options.filterArray[x] === 'exclude') {
            newList = [...newList].filter(i => !i[x] || i[x].length === 0 ? true : false)
        }
    })

    if (options.show === 'private') {
        newList = [...newList].filter(item => item.privacy && item.privacy.visibility === 'private');
    } else if (options.show === 'public') {
        newList = [...newList].filter(item => item.privacy && item.privacy.visibility === 'public');
    }

    return newList;
}

function sort<T extends Member|Group>(list: T[], options: ListOptions): T[] {
    if (options.sort === 'none')
        return list;

    let newList: T[] = [];
        if (options.sort && options.sort === 'display_name' || options.sort === 'name' || options.sort === 'id') {
            newList = [...list].sort((a, b) => {
                let aa = a[options.sort] || a.name;
                let bb = b[options.sort] || b.name;
                return aa.localeCompare(bb);
            });
        } else if (options.sort === 'pronouns') {
            newList = [...list].sort((a, b) => {
                let aa = (a as Member).pronouns;
                let bb = (b as Member).pronouns;
                if (aa === bb) return a.name.localeCompare(b.name);
                if (aa === null) return 1;
                if (bb === null) return -1;
                return aa.localeCompare(bb);
            });
        } else if (options.sort === 'birthday') {
            newList = [...list].sort((a, b) => {
                let aa = (a as Member).birthday;
                let bb = (b as Member).birthday;
                
                if (aa === bb) return a.name.localeCompare(b.name);

                if (aa === null) return 1;
                if (bb === null) return -1;

                let aBirthday = aa.slice(5, aa.length);
                let bBirthday = bb.slice(5, bb.length);

                return aBirthday.localeCompare(bBirthday);
            });
        } else if (options.sort === 'created') {
            newList = [...list].sort((a, b) => {
                let aa = a.created;
                let bb = b.created;
                
                if (aa === bb) return a.name.localeCompare(b.name);

                if (aa === null) return 1;
                if (bb === null) return -1;

                return aa.localeCompare(bb);
            });
        } else if (options.sort === 'color') {
            newList = [...list].sort((a, b) => {
                if (a.color === null) return 1;
                if (b.color === null) return -1;

                let aa = getColor(a.color);
                let bb = getColor(b.color)

                if (a.color === b.color) return a.name.localeCompare(b.name);

                if (aa.hex === "ffffff") return -1;
                if (bb.hex === "ffffff") return 1;

                if (aa.hue === bb.hue) {
                    if (aa.luma > bb.luma) return 1;
                    if (aa.luma < bb.luma) return -1;
                    return a.name.localeCompare(b.name);
                }

                if (aa.hue > bb.hue) return 1;
                if (aa.hue < bb.hue) return -1;
            })
        }
    return newList;
}

function getColor(hex: string) {
    hex = hex.replace("#", "")

    // get the RGB values
    let r = parseInt(hex.substring(0, 2), 16) / 255
    let g = parseInt(hex.substring(2, 4), 16) / 255
    let b = parseInt(hex.substring(4, 6), 16) / 255

    // Getting the Max and Min values for Chroma 
    let max = Math.max.apply(Math, [r, g, b]);
    let min = Math.min.apply(Math, [r, g, b]);
    
    
    // Variables for HSV value of hex color
    let chr = max - min;
    let hue = 0;
    let val = max;
    let sat = 0;
    
    if (val > 0) {
        // Calculate Saturation only if Value isn't 0.
        sat = chr / val;
        if (sat > 0) {
            if (r == max) {
                hue = 60 * (((g - min) - (b - min)) / chr);
                if (hue < 0) {
                    hue += 360;
                }
            } else if (g == max) {
                hue = 120 + 60 * (((b - min) - (r - min)) / chr);
            } else if (b == max) {
                hue = 240 + 60 * (((r - min) - (g - min)) / chr);
            }
        }
    }

    let color: Record<string, number|string> = {}
    color.hex = hex;
    color.chroma = chr;
    color.hue = hue;
    color.sat = sat;
    color.val = val;
    color.luma = 0.3 * r + 0.59 * g + 0.11 * b;
    color.red = parseInt(hex.substring(0, 2), 16);
    color.green = parseInt(hex.substring(2, 4), 16);
    color.blue = parseInt(hex.substring(4, 6), 16);
    return color;
}

function group(list: Member[]|Group[], groups: Group[], options: ListOptions, type?: string): Group[]|Member[] {
    let groupFilterList = [...list]

    if (options.groups.filter === "include")
        if (type === "member") {
            groupFilterList = [...list].filter(m => 
                groups.some(g => (g as Group).members.includes(m.uuid))
            );
        } else if (type === "group") {
            groupFilterList = [...list].filter((g: Group) => g.members && g.members.length > 0)
        }
    
    if (options.groups.filter === "exclude")
        if (type === "member") {
            groupFilterList = [...list].filter(m => 
                !groups.some(g => (g as Group).members.includes(m.uuid))
            );
        } else if (type === "group") {
            groupFilterList = [...list].filter((g: Group) => !g.members || g.members.length < 1)
        }
    
    let groupIncludedList = [...groupFilterList];

    if (options.groups.include.list.length > 0) {
        // include has items! check the type and whether to match exactly
        if (type === 'member')
            if (options.groups.include.exact === true)
                // match exact, include only members in EVERY group
                groupIncludedList = [...list].filter(m => 
                    (options.groups.include.list as Group[]).every(g => g.members?.includes(m.uuid))
                );
            else
            // just include any member in at least one group
            groupIncludedList = [...list].filter(m => 
                (options.groups.include.list as Group[]).some(g => g.members?.includes(m.uuid))
            );

        else if (type === 'group')
            if (options.groups.include.exact === true)
            groupIncludedList = [...list].filter(g => 
                (g as Group).members && (options.groups.include.list as Member[])
                .every(m => (g as Group).members.includes(m.id))
            );
            else 
            groupIncludedList = [...list].filter(g => 
                (g as Group).members && (options.groups.include.list as Member[])
                .some(m => (g as Group).members.includes(m.id))
            );
    }

    let groupExcludedList = [...groupIncludedList];

    if (options.groups.exclude.list.length > 0) {
        if (type === 'member')
            if (options.groups.exclude.exact === true)
                groupExcludedList = [...groupIncludedList].filter(m => 
                    !(options.groups.exclude.list as Group[]).every(g => g.members?.includes(m.uuid))
                );
            else
            groupExcludedList = [...groupIncludedList].filter(m => 
                !(options.groups.exclude.list as Group[]).some(g => g.members?.includes(m.uuid))
            );

        else if (type === 'group')
            if (options.groups.exclude.exact === true)
                groupExcludedList = [...groupIncludedList].filter(g => 
                    (g as Group).members && (options.groups.exclude.list as Member[])
                    .some(m => !(g as Group).members.includes(m.id))
                );
            else 
                groupExcludedList = [...groupIncludedList].filter(g => 
                    (g as Group).members && (options.groups.exclude.list as Member[])
                    .every(m => !(g as Group).members.includes(m.id))
                );
    }

    return groupExcludedList;
}

function reorder<T extends Member|Group>(list: T[], options: ListOptions): T[] {
    if (options.order === 'descending') {
        return [...list].reverse();
    }

    return list;
}


