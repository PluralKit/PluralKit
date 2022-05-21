---
title: Legacy API documentation
permalink: /api/legacy
---

# Legacy API documentation
::: warning
This is the documentation for v1 of the PluralKit API. Please use v2 going forwards - v1 is deprecated and will be removed eventually.

Some keys have been phased out of models and are therefore no longer functioning on v1:
- 2021-12-07: `tz` in system model always returns `UTC` (the default value). the [get system settings](/api/models#get-system-settings) v2 endpoint can be used to get the correct timezone for a system (requires authentication).
:::

## Models
The following three models (usually represented in JSON format) represent the various objects in PluralKit's API.
A `?` after the column type indicates a nullable parameter (value can be cleared by setting it to null).

### System model

| Key                   | Type     | Patchable? | Notes                                                                                     |
| --------------------- | -------- | ---------- | ----------------------------------------------------------------------------------------- |
| id                    | string   | No         |                                                                                           |
| name                  | string   | Yes        | 100-character limit.                                                                      |
| description           | string?  | Yes        | 1000-character limit.                                                                     |
| tag                   | string?  | Yes        |                                                                                           |
| color                 | string?  | Yes        | 6-char hex (eg. `ff7000`), sans `#`.                                                      |
| avatar_url            | url?     | Yes        | Not validated server-side.                                                                |
| banner                | url?     | Yes        | Not validated server-side.                                                                |
| tz                    | string?  | Yes        | Tzdb identifier. Patching with `null` will store `"UTC"`.                                 |
| created               | datetime | No         |                                                                                           |
| description_privacy   | string?  | Yes        | Patching with `private` will set it to private; `public` or `null` will set it to public. |
| member_list_privacy   | string?  | Yes        | Same as above.                                                                            |
| front_privacy         | string?  | Yes        | Same as above.                                                                            |
| front_history_privacy | string?  | Yes        | Same as above.                                                                            |

### Member model

| Key                 | Type       | Patchable?         | Notes                                                                                                                                                                               |
| ------------------- | ---------- | ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| id                  | string     | No                 |                                                                                                                                                                                     |
| name                | string     | Yes                | 50-character limit.                                                                                                                                                                 |
| display_name        | string?    | Yes                | 50-character limit.                                                                                                                                                                 |
| description         | string?    | Yes                | 1000-character limit.                                                                                                                                                               |
| pronouns            | string?    | Yes                | 100-character limit.                                                                                                                                                                |
| color               | color?     | Yes                | 6-char hex (eg. `ff7000`), sans `#`.                                                                                                                                                |
| avatar_url          | url?       | Yes                | Not validated server-side.                                                                                                                                                          |
| banner              | url?       | Yes                | Not validated server-side.                                                                                                                                                          |
| birthday            | date?      | Yes                | ISO-8601 (`YYYY-MM-DD`) format, year of `0001` or `0004` means hidden year. Birthdays set after 2020-02-10 use `0004` as a sentinel year, but both options are recognized as valid. |
| prefix              | string?    | Yes                | **Deprecated.** Use `proxy_tags` instead.                                                                                                                                           |
| suffix              | string?    | Yes                | **Deprecated.** Use `proxy_tags` instead.                                                                                                                                           |
| proxy_tags          | ProxyTag[] | Yes (entire array) | An array of ProxyTag (see below) objects, each representing a single prefix/suffix pair.                                                                                            |
| keep_proxy          | boolean    | Yes                | Whether to display a member's proxy tags in the proxied message.                                                                                                                    |
| created             | datetime   | No                 |                                                                                                                                                                                     |
| privacy             | string?    | Yes                | **Deprecated.** Use `<subject>_privacy` and `visibility` fields.                                                                                                                    |
| visibility          | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |
| name_privacy        | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |
| description_privacy | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |
| avatar_privacy      | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |
| birthday_privacy    | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |
| pronoun_privacy     | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |
| metadata_privacy    | string?    | Yes                | Patching with `private` will set it to private; `public` or `null` will set it to public.                                                                                           |

#### ProxyTag object

| Key    | Type    |
| ------ | ------- |
| prefix | string? |
| suffix | string? |

### Switch model

| Key       | Type              | Notes                                                                                                                                   |
| --------- | ----------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| timestamp | datetime          |                                                                                                                                         |
| members   | list of id/Member | Is sometimes in plain ID list form (eg. `GET /s/<id>/switches`), sometimes includes the full Member model (eg. `GET /s/<id>/fronters`). |

### Message model

| Key       | Type               | Notes                                                                                                  |
| --------- | ------------------ | ------------------------------------------------------------------------------------------------------ |
| timestamp | datetime           |                                                                                                        |
| id        | snowflake          | The ID of the message sent by the webhook. Encoded as string for precision reasons.                    |
| original  | snowflake          | The ID of the (now-deleted) message that triggered the proxy. Encoded as string for precision reasons. |
| sender    | snowflake          | The user ID of the account that triggered the proxy. Encoded as string for precision reasons.          |
| channel   | snowflake          | The ID of the channel the message was sent in. Encoded as string for precision reasons.                |
| system    | full System object | The system that proxied the message.                                                                   |
| member    | full Member object | The member that proxied the message.                                                                   |

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
    "created": "2019-01-01T14:30:00.987654Z",
    "description_privacy": "private",
    "member_list_privacy": "public",
    "front_privacy": "public",
    "front_history_privacy": "private"
}
```

### GET /s/:id
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
    "created": "2019-01-01T14:30:00.987654Z",
    "description_privacy": null,
    "member_list_privacy": null,
    "front_privacy": null,
    "front_history_privacy": null
}
```

### GET /s/:id/members
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
        "created": "2019-01-01T15:00:00.654321Z",
        "visibility": null,
        "name_privacy": null,
        "description_privacy": null,
        "birthday_privacy": null,
        "pronoun_privacy": null,
        "metadata_privacy": null
    }
]
```

### GET /s/:id/switches
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

### GET /s/:id/fronters
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
            "visibility": null,
            "name_privacy": null,
            "description_privacy": null,
            "avatar_privacy": null,
            "birthday_privacy": null,
            "pronoun_privacy": null,
            "metadata_privacy": null,
            "created": "2019-01-01T15:00:00.654321Z"
        }
    ]
}
```

### PATCH /s
**Requires authentication.**

Edits your own system's information. Missing fields will keep their current values. Will return the new system object.

#### Example request
    PATCH https://api.pluralkit.me/v1/s

```json
{
    "name": "New System Name",
    "tag": "{Sys}",
    "avatar_url": "https://path/to/new/avatar.png",
    "tz": "America/New_York",
    "description_privacy": "private",
    "member_list_privacy": "public",
    "front_privacy": "public",
    "front_history_privacy": "private"
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
    "created": "2019-01-01T14:30:00.987654Z",
    "description_privacy": "private",
    "member_list_privacy": "public",
    "front_privacy": "public",
    "front_history_privacy": "private"
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

### GET /m/:id
Queries a member's information by its 5-character member ID. If the member does not exist, will return `404 Not Found`.
If this member is marked private, and the request isn't authenticated with the member's system's token, some fields will contain `null` rather than the true value (corresponding with the privacy settings). Regardless of privacy setting, a non-authenticated request will only receive `null` for the privacy fields (and `visibility`).

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
    "created": "2019-01-01T15:00:00.654321Z",
    "visibility": "public",
    "name_privacy": "public",
    "description_privacy": "private",
    "avatar_privacy": "private",
    "birthday_privacy": "private",
    "pronoun_privacy": "public",
    "metadata_privacy": "public"
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
    "keep_proxy": false,
    "visibility": "public",
    "name_privacy": "public",
    "description_privacy": "private",
    "avatar_privacy": "private",
    "birthday_privacy": "private",
    "pronoun_privacy": "public",
    "metadata_privacy": "private"
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
    "created": "2019-01-01T15:00:00.654321Z",
    "visibility": "public",
    "name_privacy": "public",
    "description_privacy": "private",
    "birthday_privacy": "private",
    "pronoun_privacy": "public",
    "metadata_privacy": "private"
}
```

### PATCH /m/:id
**Requires authentication.**

Edits a member's information. Missing fields will keep their current values. Will return the new member object. Member must (obviously) belong to your own system.

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
    "keep_proxy": false,
    "visibility": "public",
    "name_privacy": "public",
    "description_privacy": "private",
    "avatar_privacy": "private",
    "birthday_privacy": "private",
    "pronoun_privacy": "public",
    "metadata_privacy": "private"
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
    "created": "2019-01-01T15:00:00.654321Z",
    "visibility": "public",
    "name_privacy": "public",
    "description_privacy": "private",
    "avatar_privacy": "private",
    "birthday_privacy": "private",
    "pronoun_privacy": "public",
    "metadata_privacy": "private"
}
```

### DELETE /m/:id
**Requires authentication.**

Deletes a member from the database. Be careful as there is no confirmation and the member will be deleted immediately. Member must (obviously) belong to your own system.

#### Example request
    DELETE https://api.pluralkit.me/v1/m/qwert

#### Example response
(`204 No Content`)

### GET /a/:id
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
    "created": "2019-01-01T14:30:00.987654Z",
    "description_privacy": null,
    "member_list_privacy": null,
    "front_privacy": null,
    "front_history_privacy": null
}
```

### GET /msg/:id
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
        "created": "2019-01-01T15:00:00.654321Z",
        "visibility": "public",
        "name_privacy": "public",
        "description_privacy": "private",
        "avatar_privacy": "private",
        "birthday_privacy": "private",
        "pronoun_privacy": "public",
        "metadata_privacy": "private"
    }
}
```