import axios from 'axios';
import * as Sentry from '@sentry/browser';

const baseUrl = () => localStorage.isBeta ? "https://api.beta.pluralkit.me" : "https://api.pluralkit.me";

const methods = ['get', 'post', 'delete', 'patch', 'put'];
const noop = () => {};

const scheduled = [];
const runAPI = () => {
    if (scheduled.length == 0) return;
    const {axiosData, res, rej} = scheduled.shift();
    axios(axiosData)
        .then((resp) => res(parseData(resp.status, resp.data)))
        .catch((err) => {
            Sentry.captureException("Fetch error", err);
            rej(err);
        });
}

setInterval(runAPI, 500);

export default function() {
    const route = [];
    const handler = {
        get(_, name) {
            if (route.length == 0 && name != "private")
                route.push("v2");
            if (methods.includes(name)) {
                return ({ data = undefined, auth = true, token = null, query = null } = {}) => new Promise((res, rej) => scheduled.push({ res, rej, axiosData: {
                    url: baseUrl() + "/" + route.join("/") + (query ? `?${Object.keys(query).map(x => `${x}=${query[x]}`).join("&")}` : ""),
                    method: name,
                    headers: {
                        authorization: token ?? (auth ? localStorage.getItem("pk-token") : undefined),
                        "content-type": name == "get" ? undefined : "application/json"
                    },
                    data: !!data ? JSON.stringify(data) : undefined,
                    validateStatus: () => true,
                }}));
            }
            route.push(name);
            return new Proxy(noop, handler);
        },
        apply(target, _, args) {
            route.push(...args.filter(x => x != null));
            return new Proxy(noop, handler);
        }
    }
    return new Proxy(noop, handler);
}

import * as errors from './errors';

function parseData(code: number, data: any) {
    if (code == 200) return data;
    if (code == 204) return;
    throw errors.parse(code, data);
}