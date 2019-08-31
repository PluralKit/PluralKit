---
layout: default
title: API documentation
permalink: /api
description: PluralKit's API documentation.
---

# API documentation
PluralKit has a basic HTTP REST API for querying and modifying your system.
The root endpoint of the API is `https://api.pluralkit.me/v1/`.

Endpoints will always return all fields, using `null` when a value is missing. On `PATCH` endpoints, you *must* include
all fields, too. Missing fields will be interpreted as `null`, and `null` fields will have their value removed. To
preserve a value, pass the existing value again.

## Authentication
Authentication is done with a simple "system token". You can get your system token by running `pk;token` using the
Discord bot, either in a channel with the bot or in DMs. Then, pass this token in the `Authorization` HTTP header
on requests that require it. Failure to do so on endpoints that require authentication will return a `401 Unauthorized`.

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
|description|string?|Yes|1000-character limit.|
|color|color?|Yes|6-char hex (eg. `ff7000`), sans `#`.|
|avatar_url|url?|Yes|Not validated server-side.|
|birthday|date?|Yes|ISO-8601 (`YYYY-MM-DD`) format, year of `0001` means hidden year.|
|prefix|string?|Yes||
|suffix|string?|Yes||
|created|datetime|No||

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
        "prefix": "[",
        "suffix": "]",
        "created": "2019-01-01T15:00:00.654321Z"
    }
]
```

### GET /s/\<id>/switches[?before=<timestamp>]
Returns a system's switch history in newest-first chronological order, with a maximum of 100 switches. If the system doesn't exist, returns `404 Not Found`.
Optionally takes a `?before=` query parameter with an ISO-8601-formatted timestamp, and will only return switches
that happen before that timestamp.

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
            "prefix": "[",
            "suffix": "]",
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
    "avatar_url": "https://path/to/new/avatar.png"
    "tz": "America/New_York"
}
```
(note the absence of a `description` field, which is set to null in the response)

#### Example response
```json
{
    "id": "abcde",
    "name": "New System Name",
    "description": null,
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
    "prefix": "[",
    "suffix": "]",
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
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "prefix": "["
}
```
(note the absence of a `suffix` field, which is set to null in the response)

#### Example response
```json
{
    "id": "qwert",
    "name": "Craig Peterson",
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "prefix": "[",
    "suffix": null,
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
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "prefix": "["
}
```
(note the absence of a `suffix` field, which is set to null in the response)

#### Example response
```json
{
    "id": "qwert",
    "name": "Craig Peterson",
    "color": null,
    "avatar_url": "https://path/to/new/image.png",
    "birthday": "1997-07-14",
    "pronouns": "they/them",
    "description": "I am Craig, cooler example user extraordinaire.",
    "prefix": "[",
    "suffix": null,
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

#### Example request
    GET https://api.pluralkit.me/v1/msg/601014599386398700

#### Example response
```json
{
    "timestamp": "2019-07-17T11:37:26.805Z",
    "id": "601014599386398700",
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
        "prefix": "[",
        "suffix": "]",
        "created": "2019-01-01T15:00:00.654321Z"
    }
}
```

## Version history
* 2019-07-17
  * Add endpoint for querying system by account
  * Add endpoint for querying message contents
* 2019-07-10 **(v1)**
  * First specified version
* (prehistory)
  * Initial release
