---
title: Dispatch
permalink: /api/dispatch
---

# Dispatch Webhooks

Dispatch webhooks are a way to get notified when you update your system information on PluralKit. It can be used for integrations where you want to perform some action when you run a bot command on Discord, but also don't want to (or can't) set up a Discord bot to listen to messages.

You will need a publicly-accessible webserver that can receive and process JSON-formatted POST requests.

## Security

::: warning
On the internet, security is important! Don't skip this section.
:::

To get dispatch events from PluralKit, you must set up a *public* HTTP endpoint. As such, anyone who knows the URL to the endpoint - **not only PluralKit** - can send POST requests and "pretend" to be PluralKit.

For this reason, when you register a webhook URL, PluralKit generates a secret token, and then includes it with every event sent to you in the `signing_token` key. If you receive an event with an invalid `signing_token`, you **must** stop processing the request and **respond with a 401 status code**.

PluralKit will send invalid requests to your endpoint, with `PING` event type, once in a while to confirm that you are correctly validating requests.

## Dispatch Event Model

|key|type|description|
|---|---|---|
|type|string|[event type](#dispatch-events)|
|signing_token|string|the [signing token](#security) for your webhook URL|
|system_id|string|the system ID associated with this event|
|id|string?|the ID of the entity referenced by the event (can be a system/member/group/switch/Discord user ID)|
|data|object?|event data|

## Dispatch Events

|name|description|content of `data` object|notes|
|---|---|---|---|
|PING|PluralKit is checking if your webhook URL is working.|null|Reply with a 200 status code if the `signing_token` is correct, or a 401 status code if it is invalid.|
|UPDATE_SYSTEM|your system was updated|[system object](/api/models#system-model) only containing modififed keys|
|UPDATE_SETTINGS|your bot settings were updated|[system settings object](/api/models#system-settings-model) only containing modified keys|
|CREATE_MEMBER|a new member was created|[member object](/api/models#member-model) only containing `name` key|new member ID can be found in the top-level `id` key`|
|UPDATE_MEMBER|a member was updated|[member object](/api/models#member-model) only containing modified keys|member ID can be found in the top-level `id` key`|
|DELETE_MEMBER|a member was deleted|null|old member ID can be found in the top-level `id` key`|
|CREATE_GROUP|a new group was created|[group object](/api/models#group-model) only containing `name` key|new group ID can be found in the top-level `id` key`|
|UPDATE_GROUP|a group was updated|[group object](/api/models#group-model) only containing modified keys|group ID can be found in the top-level `id` key`|
|UPDATE_GROUP_MEMBERS|the member list of a group was updated|unknown|This event is currently non-functional|
|DELETE_GROUP|a group was deleted|null|old group ID can be found in the top-level `id` key`|
|LINK_ACCOUNT|a new Discord account was linked to your system|null|new account ID can be found in the top-level `id` key|
|UNLINK_ACCOUNT|a Discord account was unlinked from your system|null|old account ID can be found in the top-level `id` key|
|UPDATE_SYSTEM_GUILD|your system settings in a specific server were updated|[system guild settings](/api/models#system-guild-settings-model) with only modified keys|
|UPDATE_MEMBER_GUILD|the settings for a member in a specific server were updated|[member guild settings](/api/models#member-guild-settings-model) with only modified keys|
|CREATE_MESSAGE|a message was sent|[message object](/api/models#message-model)|
|CREATE_SWITCH|a new switch was logged|[switch object](/api/models#switch-model)|
|UPDATE_SWITCH|a switch was updated|[switch object](/api/models#switch-model) with only modified keys|
|DELETE_SWITCH|a switch was deleted|null|old switch ID can be found in top-level `id` key|
|DELETE_ALL_SWITCHES|your system's switches were bulk deleted|null|
|SUCCESSFUL_IMPORT|some information was successfully imported through the `pk;import` command to your system|null|
