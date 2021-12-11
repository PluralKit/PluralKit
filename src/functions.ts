import { currentUser, loggedIn } from "./stores";
import PKAPI from "./api";
import type Sys from './api/system';


function blockQuote(text: string) {
    let match = text.match(/(?<=\n|^)(> [^\n]*(?:\n>[^\n]*)*)/gim);
    let parse: string[] = [];
    for (let i = 0; i < match.length; i++) {
        parse[i] = match[i].replace(/(?<=\n|^)> ?/gim, "");
        text = text.replace(match[i], `<div class="bq">${parse[i]}</div>`);
    }
    return text;
}

export function parseMarkdown(text: string) {
    text = blockQuote(text);
    text = text.replace(/\*{3}(.*?)\*{3}/gim, '<b><i>$1</i></b>');
    text = text.replace(/\*{2}(.*?)\*{2}/gim, '<b>$1</b>');
    text = text.replace(/\*{1}(.*?)\*{1}/gim, '<i>$1</i>');
    text = text.replace(/\n/gim, '<br />')
    return text;
}