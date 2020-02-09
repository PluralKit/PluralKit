---
layout: default
title: API documentation
permalink: /api
description: PluralKit's API documentation.
nav_order: 4
---

# API documentation
PluralKit has a basic HTTP REST API for querying and modifying your system.
The root endpoint of the API is `https://api.pluralkit.me/v1/`.

Endpoints will always return all fields, using `null` when a value is missing. On `PATCH` endpoints,
missing fields from the JSON request will be ignored and preserved as is, but on `POST` endpoints will
be set to `null` or cleared.

Endpoints taking JSON bodies (eg. most `PATCH` and `PUT` endpoints) require the `Content-Type: application/json` header set.

## Authentication
Authentication is done with a simple "system token". You can get your system token by running `pk;token` using the
Discord bot, either in a channel with the bot or in DMs. Then, pass this token in the `Authorization` HTTP header
on requests that require it. Failure to do so on endpoints that require authentication will return a `401 Unauthorized`.

Some endpoints show information that a given system may have set to private. If this is a specific field
(eg. description), the field will simply contain `null` rather than the true value. If this applies to entire endpoint
responses (eg. fronter, switches, member list), the entire request will return `403 Forbidden`. Authenticating with the
system's token (as described above) will override these privacy settings and show the full information. 

## Models
The following three models (usually represented in JSON format) represent the various objects in PluralKit's API. A `?` after the column type indicates an optional (nullable) parameter.

### System model

|Key|Type|Patchable?|Notes|
|---|---|---|---|
|id|string|No||
|name|string?|Yes|100-character limit.|
|description|string?|Yes|1000-character limit.|
|tag|string?|Yes||
|avatar_url|url?|Yes|Not validated server-side.|
|tz|string?|Yes|Tzdb identifier. Patching with `null` will store `"UTC"`.|
|created|datetime|No||

### Member model

|Key|Type|Patchable?|Notes|
|---|---|---|---|
|id|string|No||
|name|string?|Yes|50-character limit.|
|display_name|string?|Yes|50-character limit.|
|description|string?|Yes|1000-character limit.|
|color|color?|Yes|6-char hex (eg. `ff7000`), sans `#`.|
|avatar_url|url?|Yes|Not validated server-side.|
|birthday|date?|Yes|ISO-8601 (`YYYY-MM-DD`) format, year of `0001` or `0004` means hidden year. Birthdays set after 2020-02-10 use `0004` as a sentinel year, but both options are recognized as valid.|
|prefix|string?|Yes|Deprecated. Use `proxy_tags` instead.|
|suffix|string?|Yes|Deprecated. Use `proxy_tags` instead.|
|proxy_tags|ProxyTag[]|Yes (entire array)|An array of ProxyTag (see below) objects, each representing a single prefix/suffix pair.|
|keep_proxy|bool|Yes|Whether to display a member's proxy tags in the proxied message.|
|created|datetime|No||

#### ProxyTag object

|Key|Type|
|---|---|
|prefix|string?|
|suffix|string?|

### Switch model

|Key|Type|Notes|
|---|---|---|
|timestamp|datetime||
|members|list of id/Member|Is sometimes in plain ID list form (eg. `GET /s/<id>/switches`), sometimes includes the full Member model (eg. `GET /s/<id>/fronters`).|

### Message model

|Key|Type|Notes|
|---|---|---|
|timestamp|datetime||
|id|snowflake|The ID of the message sent by the webhook. Encoded as string for precision reasons.|
|original|snowflake|The ID of the (now-deleted) message that triggered the proxy. Encoded as string for precision reasons.|
|sender|snowflake|The user ID of the account that triggered the proxy. Encoded as string for precision reasons.|
|channel|snowflake|The ID of the channel the message was sent in. Encoded as string for precision reasons.|
|system|full System object|The system that proxied the message.|
|member|full Member object|The member that proxied the message.|

## Endpoints

### GET /s
**Requires authentication.**

Returns information about your own system.

#### Example request
    GET https://api.pluralkit.me/v1/s

#### Example response
```json
{
    "id": "abcde",
    "name": "My System",
    "description": "This is my system description. Yay.",
    "tag": "[MySys]",
    "avatar_url": "https://path/to/avatar/image.png",
    "tz": "Europe/Copenhagen",
    "created": "2019-01-01T14:30:00.987654Z"
}
```

### GET /s/\<id>
Queries a system by its 5-character ID, and returns information about it. If the system doesn't exist, returns `404 Not Found`.
Some fields may be set to `null` if unauthenticated and the system has chosen to make those fields private.

#### Example request
    GET https://api.pluralkit.me/v1/s/abcde

#### Example response
```json
{
    "id": "abcde",
    "name": "My System",
    "description": "This is my system description. Yay.",
    "tag": "[MySys]",
    "avatar_url": "https://path/to/avatar/image.png",
    "tz": "Europe/Copenhagen",
    "created": "2019-01-01T14:30:00.987654Z"
}
```

### GET /s/\<id>/members
Queries a system's member list by its 5-character ID. If the system doesn't exist, returns `404 Not Found`.
If the system has chosen to hide its member list, this will return `403 Forbidden`, unless the request is authenticated with the system's token.
If the request is not authenticated with the system's token, members marked as private will *not* be returned.

#### Example request
    GET https://api.pluralkit.me/v1/s/abcde/members

#### Example response
```json
[
    {
        "id": "qwert",
        "name": "Craig Johnson",
        "color": "ff7000",
        "avatar_url": "https://path/to/avatar/image.png",
        "birthday": "1997-07-14",
        "pronouns": "he/him or they/them",
        "description": "I am Craig, example user extraordinaire.",
        "proxy_tags": [{"prefix": "[", "suffix": "]"}],
        "keep_proxy": false,
        "created": "2019-01-01T15:00:00.654321Z"
    }
]
```

### GET /s/\<id>/switches[?before=<timestamp>]
Returns a system's switch history in newest-first chronological order, with a maximum of 100 switches. If the system doesn't exist, returns `404 Not Found`.
Optionally takes a `?before=` query parameter with an ISO-8601-formatted timestamp, and will only return switches
that happen before that timestamp.

If the system has chosen to hide its switch history, this will return `403 Forbidden`, unless the request is authenticated with the system's token.

#### Example request
    GET https://api.pluralkit.me/v1/s/abcde/switches?before=2019-03-01T14:00:00Z

#### Example response
```json
[
    {
        "timestamp": "2019-02-23T14:20:59.123456Z",
        "members": ["qwert", "yuiop"]
    },
    {
        "timestamp": "2019-02-22T12:00:00Z",
        "members": ["yuiop"]
    },
    {
        "timestamp": "2019-02-20T09:30:00Z",
        "members": []
    }
]
```

### GET /s/\<id>/fronters
Returns a system's current fronter(s), with fully hydrated member objects. If the system doesn't exist, *or* the system has no registered switches, returns `404 Not Found`.
If the system has chosen to hide its current fronters, this will return `403 Forbidden`, unless the request is authenticated with the system's token. If a returned member is private, and the request isn't properly authenticated, some fields may be null.

#### Example request
    GET https://api.pluralkit.me/v1/s/abcde/fronters

#### Example response
```json
{
    "timestamp": "2019-07-09T17:22:46.47441Z",
    "members": [
        {
            "id": "qwert",
            "name": "Craig Johnson",
            "color": "ff7000",
            "avatar_url": "https://path/to/avatar/image.png",
            "birthday": "1997-07-14",
            "pronouns": "he/him or they/them",
            "description": "I am Craig, example user extraordinaire.",
            "proxy_tags": [{"prefix": "[", "suffix": "]"}],
            "keep_proxy": false,
            "created": "2019-01-01T15:00:00.654321Z"
        }
    ]
}
```

### PATCH /s
**Requires authentication.**

Edits your own system's information. Missing fields will be set to `null`. Will return the new system object.

#### Example request
    PATCH https://api.pluralkit.me/v1/s

```json
{
    "name": "New System Name",
    "tag": "{Sys}",
    "avatar_url": "https://path/to/new/avatar.png",
    "tz": "America/New_York"
}
```
(note the absence of a `description` field, which has its old value preserved in the response)

#### Example response
```json
{
    "id": "abcde",
    "name": "New System Name",
    "description": "The Old Description, Not Updated",
    "tag": "{Sys}",
    "avatar_url": "https://path/to/new/avatar.png",
    "tz": "America/New_York",
    "created": "2019-01-01T14:30:00.987654Z"
}
```

### POST /s/switches
**Requires authentication.**

Registers a new switch to your own system given a list of member IDs.

#### Example request
    POST https://api.pluralkit.me/v1/s/switches

```json
{
    "members": ["qwert", "yuiop"]
}
```

#### Example response
(`204 No Content`)

### GET /m/\<id>
Queries a member's information by its 5-character member ID. If the member does not exist, will return `404 Not Found`.
If this member is marked private, and the request isn't authenticated with the member's system's token, some fields (currently only `description`) will contain `null` rather than the true value.

#### Example request
    GET https://api.pluralkit.me/v1/m/qwert

#### Example response
```json
{
    "id": "qwert",
    "name": "Craig Johnson",
    "color": "ff7000",
    "avatar_url": "https://path/to/avatar/image.png",
    "birthday": "1997-07-14",
    "pronouns": "he/him or they/them",
    "description": "I am Craig, example user extraordinaire.",
    "proxy_tags": [{"prefix": "[", "suffix": "]"}],
    "keep_proxy": false,
    "created": "2019-01-01T15:00:00.654321Z"
}
```

### POST /m
**Requires authentication.**

Creates a new member with the information given. Missing fields (except for name) will be set to `null`. Will return the new member object. Member must (obviously) belong to your own system.

#### Example request
    POST https://api.pluralkit.me/v1/m

```json
{
    "name": "Craig Peterson",
    "display_name": "Craig Peterson [he/they]",
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "keep_proxy": false
}
```
(note the absence of a `proxy_tags` field, which is cleared in the response)

#### Example response
```json
{
    "id": "qwert",
    "name": "Craig Peterson",
    "display_name": "Craig Peterson [he/they]",
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "proxy_tags": [],
    "keep_proxy": false,
    "created": "2019-01-01T15:00:00.654321Z"
}
```

### PATCH /m/\<id>
**Requires authentication.**

Edits a member's information. Missing fields will be set to `null`. Will return the new member object. Member must (obviously) belong to your own system.

#### Example request
    PATCH https://api.pluralkit.me/v1/m/qwert

```json
{
    "name": "Craig Peterson",
    "display_name": "Craig Peterson [he/they]",
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "keep_proxy": false
}
```
(note the absence of a `proxy_tags` field, which keeps its old value in the response)

#### Example response
```json
{
    "id": "qwert",
    "name": "Craig Peterson",
    "display_name": "Craig Peterson [he/they]",
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "proxy_tags": [{"prefix": "[", "suffix": "]"}],
    "keep_proxy": false,
    "created": "2019-01-01T15:00:00.654321Z"
}
```

### DELETE /m/\<id>
**Requires authentication.**

Deletes a member from the database. Be careful as there is no confirmation and the member will be deleted immediately. Member must (obviously) belong to your own system.

#### Example request
    DELETE https://api.pluralkit.me/v1/m/qwert

#### Example response
(`204 No Content`)

### GET /a/\<id>
Queries a system by its linked Discord account ID (17/18-digit numeric snowflake). Returns `404 Not Found` if the account doesn't have a system linked.
Some fields may be set to `null` if unauthenticated and the system has chosen to make those fields private.

#### Example request
    GET https://api.pluralkit.me/v1/a/466378653216014359

#### Example response
```json
{
    "id": "abcde",
    "name": "My System",
    "description": "This is my system description. Yay.",
    "tag": "[MySys]",
    "avatar_url": "https://path/to/avatar/image.png",
    "tz": "Europe/Copenhagen",
    "created": "2019-01-01T14:30:00.987654Z"
}
```

### GET /msg/\<id>
Looks up a proxied message by its message ID. Returns `404 Not Found` if the message ID is invalid or wasn't found (eg. was deleted or not proxied by PK).
You can also look messages up by their *trigger* message ID (useful for, say, logging bot integration).

The returned system and member's privacy settings will be respected, and as such, some fields may be set to null without the proper authentication.

#### Example request
    GET https://api.pluralkit.me/v1/msg/601014599386398700

#### Example response
```json
{
    "timestamp": "2019-07-17T11:37:26.805Z",
    "id": "601014599386398700",
    "original": "601014598168435600",
    "sender": "466378653216014359",
    "channel": "471388251102380000",
    "system": {
        "id": "abcde",
        "name": "My System",
        "description": "This is my system description. Yay.",
        "tag": "[MySys]",
        "avatar_url": "https://path/to/avatar/image.png",
        "tz": "Europe/Copenhagen",
        "created": "2019-01-01T14:30:00.987654Z"
    },
    "member": {
        "id": "qwert",
        "name": "Craig Johnson",
        "color": "ff7000",
        "avatar_url": "https://path/to/avatar/image.png",
        "birthday": "1997-07-14",
        "pronouns": "he/him or they/them",
        "description": "I am Craig, example user extraordinaire.",
        "proxy_tags": [{"prefix": "[", "suffix": "]"}],
        "keep_proxy": false,
        "created": "2019-01-01T15:00:00.654321Z"
    }
}
```

## Version history
* 2020-02-10
  * Birthdates with no year can now be stored using `0004` as a year, for better leap year support. Both options remain valid and either may be returned by the API.
* 2020-01-08
  * Added privacy support, meaning some responses will now lack information or return 403s, depending on the specific system and member's privacy settings.
* 2019-12-28
  * Changed behaviour of missing fields in PATCH responses, will now preserve the old value instead of clearing
  * This is technically a breaking change, but not *significantly* so, so I won't bump the version number.
* 2019-10-31
  * Added `proxy_tags` field to members
  * Added `keep_proxy` field to members
  * Deprecated `prefix` and `suffix` member fields, will be removed at some point (tm)
* 2019-07-17
  * Added endpoint for querying system by account
  * Added endpoint for querying message contents
* 2019-07-10 **(v1)**
  * First specified version
* (prehistory)
  * Initial release
