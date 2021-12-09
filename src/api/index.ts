import axios, { AxiosInstance, Method, AxiosResponse, AxiosRequestConfig } from 'axios';
import Sys from './system';
import Member from './member';
import Group from './group';


type FieldError = {
    message: string
}

export default class PKAPI {

    ROUTES = {
        GET_SYSTEM: (sid?: string) => sid ? `/systems/${sid}` : `/systems/@me`,
        GET_MEMBER_LIST: (sid?: string) => sid ? `/systems/${sid}/members` : `/systems/@me/members`,
        GET_MEMBER: (mid: string) => `/members/${mid}`,
        GET_GROUP_LIST: (sid?: string) => sid ? `/systems/${sid}/groups` : `/systems/@me/groups`,

        PATCH_SYSTEM: () => `/systems/@me`,
        
        POST_MEMBER: () => `/members`
    }

    baseUrl: string;
    instance: AxiosInstance
    
    constructor(baseUrl?: string) {
        this.baseUrl = baseUrl || 'https://api.pluralkit.me';

        this.instance = axios.create({
            baseURL: this.baseUrl + '/v2'
        })
    }

    async getSystem(options: { token?: string, id?: any}) {
        if (!options.token && !options.id) {
            throw new Error("Must pass a token or id.")
        }
        var system: Sys;
        var res: AxiosResponse;
        try {
            res = await this.handle(this.ROUTES.GET_SYSTEM(options.id ? options.id : ""), 'GET', {token: !options.id ? options.token : ""});
            if (res.status === 200) system = new Sys(res.data);
            else this.handleErrors(res);
        } catch (error) {
            throw new Error(error.message);
        }
        return system;
    }

    async patchSystem(options: {token: string, data: any}) {
        var body = new Sys(options.data);
        var system: Sys;
        var res: AxiosResponse;
        try {
            res = await this.handle(this.ROUTES.PATCH_SYSTEM(), 'PATCH', {token: options.token, body: body});
            if (res.status === 200) system = new Sys(res.data);
            else this.handleErrors(res);
        } catch (error) {
            throw new Error(error.message);
        }
        return system;
    }

    async getMemberList(options: { token?: string, id?: any}) {
        if (!options.token && !options.id) {
            throw new Error("Must pass a token or id.")
        }
        var members: Member[] = [];
        var res: AxiosResponse;
        try {
            res = await this.handle(this.ROUTES.GET_MEMBER_LIST(options.id ? options.id : ""), 'GET', {token: !options.id ? options.token : ""});
            if (res.status === 200) {
                let resObject: any = res.data;
                resObject.forEach(m => {
                    let member = new Member(m);
                    members.push(member);
                })
            }
            else this.handleErrors(res);
        } catch (error) {
            throw new Error(error.message);
        }
        return members;
    }

    async getMember(options: {id: any}) {
        if (!options.id) {
            throw new Error("Must pass an id.")
        }
        var member: Member;
        var res: AxiosResponse;
        try {
            res = await this.handle(this.ROUTES.GET_MEMBER(options.id), 'GET', {});
            if (res.status === 200) member = new Member(res.data);
            else this.handleErrors(res);
        } catch (error) {
            throw new Error(error.message);
        }
        return member;
    }

    async postMember(options: {token: any, data: any}) {
        if (!options.token) throw new Error("Must pass a token.");
        var body = new Member(options.data);
        var member: Member;
        var res: AxiosResponse;
        try {
            res = await this.handle(this.ROUTES.POST_MEMBER(), 'POST', {token: options.token, body: body});
            if (res.status === 200) member = new Member(res.data);
            else this.handleErrors(res);
        } catch (error) {
            throw new Error(error.message);
        }
        return member;
    }

    async getGroupList(options: {token?: string, id?: any}) {
        if (!options.token && !options.id) {
            throw new Error("Must pass a token or id.");
        }
        var groups: Group[] = [];
        var res: AxiosResponse;
        try {
            res = await this.handle(this.ROUTES.GET_GROUP_LIST(options.id ? options.id : ""), 'GET', {token: !options.id ? options.token : ""});
                if (res.status === 200) {
                    let resObject: any = res.data;
                    resObject.forEach(g => {
                        let group = new Group(g);
                        groups.push(group);
                    })
                }
                else this.handleErrors(res);
        } catch (error) {
            throw new Error(error.message);
        }
        return groups;
    }

    handleErrors(res: any) {
        if (res.status === 500) throw new Error("500: Internal server error.");
        else if (res.status === 401) throw new Error("401: Your token is invalid.");
        else {
            let errorObject: any = res.data
            if (errorObject.code) {
                if (errorObject.code === 40001) {
                    var message: string;
                    for (var key in errorObject.errors) {
                        var val = errorObject.errors[key];
                    }
                } else {
                    throw new Error(errorObject.message);
                }
            }
        }
    }

    async handle(url: string, method: Method, options: {token?: string, body?: object}) {
        var headers = {}
        var request: AxiosRequestConfig = {url, method, headers}

        if(options.token) request.headers["Authorization"] = options.token;
        if (options.body) {
            request.headers["Content-Type"] = "application/json";
            request.data = JSON.stringify(options.body);
        }

        try {
            var res = await this.instance(request);
        } catch (error) {
            res = error.response;
        }
        return res;
    }

}