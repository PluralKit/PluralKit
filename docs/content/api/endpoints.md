---
name: Endpoints
permalink: /api/endpoints
---

# Endpoints

The base URL for the PluralKit API is `https://api.pluralkit.me/v2`. Endpoint URLs should be added to the base URL to get a full URL to query.

All query string parameters are optional, but if present they require a non-null value.

---
## Systems

*`systemRef` can be a system's short (5 or 6 character) ID, a system's UUID, the ID of a Discord account linked to the system, or the string `@me` to refer to the currently authenticated system.*

### Get System

GET `/systems/{systemRef}`

Returns a [system object](/api/models#system-model).

### Update System

PATCH `/systems/{systemRef}`

Takes a partial [system object](/api/models#system-model).

Returns a [system object](/api/models#system-model).

### Get System Settings

GET `/systems/{systemRef}/settings`

Returns a [system settings object](/api/models#system-settings-model).

If not authenticated, or authenticated as a different system, returns a [public system settings object](/api/models#public-system-settings-model).

### Update System Settings

PATCH `/systems/{systemRef}/settings`

Takes a partial [system settings object](/api/models#system-settings-model).

Returns a [system settings object](/api/models#system-settings-model).

### Get System Guild Settings

GET `/systems/@me/guilds/{guild_id}`

Returns a [system guild settings](/api/models#system-guild-settings-model) object.

::: tip
You must already have updated per-guild settings for your system in the target guild before being able to get or update them from the API.
:::

### Update System Guild Settings

PATCH `/systems/@me/guilds/{guild_id}`

Takes a partial [system guild settings](/api/models#system-guild-settings-model) object.

Returns a [system guild settings](/api/models#system-guild-settings-model) object on success.

### Get System Autoproxy Settings

GET `/systems/@me/autoproxy`

Query String Parameters
|name|type|
|---|---|
|guild_id?|snowflake|
|channel_id?|snowflake|

Returns an [autoproxy settings](/api/models/#autoproxy-settings-model) object on success.

::: warning
Currently, only autoproxy with `guild_id` is supported. The API will return an error message if you specify `channel_id`, or do not specify a `guild_id`.
:::

### Update System Autoproxy Settings

PATCH `/systems/@me/autoproxy`

Query String Parameters
|name|type|
|---|---|
|guild_id?|snowflake|
|channel_id?|snowflake|

Takes a partial [autoproxy settings](/api/models/#autoproxy-settings-model) object.

Returns an [autoproxy settings](/api/models/#autoproxy-settings-model) object on success.

::: warning
Currently, only autoproxy with `guild_id` is supported. The API will return an error message if you specify `channel_id`, or do not specify a `guild_id`.
:::

---
## Members

*`memberRef` can be a member's short (5 or 6 character ID) or a member's UUID.*

### Get System Members

GET `/systems/{systemRef}/members`

Returns a list of [member objects](/api/models#member-model).

### Create Member

POST `/members`

Takes a partial [member object](/api/models#member-model) as input. Key `name` is required.

Returns a [member object](/api/models#member-model) on success.

### Get Member

GET `/members/{memberRef}`

Returns a [member object](/api/models#member-model).

### Update Member

PATCH `/members/{memberRef}`

Takes a partial [member object](/api/models#member-model) as input.

Returns a [member object](/api/models#member-model) on success.

### Delete Member

DELETE `/members/{memberRef}`

Returns 204 No Content on success.

### Get Member Groups

GET `/members/{memberRef}/groups`

Returns an array of [group objects](/api/models/#group-model).

### Add Member To Groups

POST `/members/{memberRef}/groups/add`

Takes a list of group references as input. Returns 204 No Content on success.

### Remove Member From Groups

POST `/members/{memberRef}/groups/remove`

::: tip
If you want to remove *all* groups from a member, consider using the [Overwrite Member Groups](#overwrite-member-groups) endpoint instead.
:::

Takes a list of group references as input. Returns 204 No Content on success.

### Overwrite Member Groups

POST `/members/{memberRef}/groups/overwrite`

Takes a list of group references as input. (An empty list is accepted.) Returns 204 No Content on success.

### Get Member Guild Settings

GET `/members/{memberRef}/guilds/{guild_id}`

Returns a [member guild settings](/api/models#member-guild-settings-model) object.

::: tip
You must already have updated per-guild settings for the target member in the target guild before being able to get or update them from the API.
:::

### Update Member Guild Settings

PATCH `/members/{memberRef}/guilds/{guild_id}`

Takes a partial [member guild settings](/api/models#member-guild-settings-model) object.

Returns a [member guild settings](/api/models#member-guild-settings-model) object on success.

---
## Groups

*`groupRef` can be a group's short (5 or 6 character ID) or a group's UUID.*

### Get System Groups

GET `/systems/{systemRef}/groups`

Query String Parameters
|name|type|description
|---|---|---|
|with_members|boolean|includes `members` key with array of member UUIDs in each group object|

Returns a list of [group objects](/api/models/#group-model).

### Create Group

POST `/groups`

Takes a partial [group object](/api/models#group-model) as input. Key `name` is required.

Returns a [group object](/api/models#group-model) on success, or an error object on failure.

### Get Group

GET `/groups/{groupRef}`

Returns a [group object](/api/models/#group-model).

### Update Group

PATCH `/groups/{groupRef}`

Takes a partial [group object](/api/models#group-model) as input.

Returns a [group object](/api/models#group-model) on success, or an error object on failure.

### Delete Group

DELETE `/groups/{groupRef}`

Returns 204 No Content on success.

### Get Group Members

GET `/groups/{groupRef}/members`

Returns an array of [member objects](/api/models#member-model).

### Add Members To Group

POST `/groups/{groupRef}/members/add`

Takes an array of member references as input. Returns 204 No Content on success.

### Remove Member From Group

POST `/groups/{groupRef}/members/remove`

::: tip
If you want to remove *all* members from a group, consider using the [Overwrite Group Members](#overwrite-group-members) endpoint instead.
:::

Takes an array of member references as input. Returns 204 No Content on success.

### Overwrite Group Members

POST `/groups/{groupRef}/members/overwrite`

Takes an array of member references as input. (An empty list is accepted.) Returns 204 No Content on success.

---
## Switches

*`switchRef` must be a switch's UUID. `systemRef` can be a system's short (5 or 6 character) ID, a system's UUID, the ID of a Discord account linked to the system, or the string `@me` to refer to the currently authenticated system.*


### Get System Switches

GET `/systems/{systemRef}/switches`

Query String Parameters

|key|type|description|
|---|---|---|
|before|timestamp|date to get latest switch from|
|limit|int|number of switches to get (defaults to 100)||
::: warning
This endpoint returns at most 100 switches. To get more switches, make multiple requests using the `before` parameter for pagination.
:::

Returns a [switch object](/api/models#switch-model) containing a list of IDs.

### Get Current System Fronters

GET `/systems/{systemRef}/fronters`

Returns a [switch object](/api/models#switch-model) containing a list of member objects.

If the target system has no registered switches, returns 204 status code with no content.

### Create Switch

POST `/systems/{systemRef}/switches`

JSON Body Parameters

|key|type|description|
|---|---|---|
|?timestamp|datetime*|when the switch started|
|members|list of strings**|members present in the switch (or empty list for switch-out)|

\* Defaults to "now" when missing.

** Can be short IDs or UUIDs.

Returns a [switch object](/api/models#switch-model) containing a list of member objects.

### Get Switch

GET `/systems/{systemRef}/switches/{switchRef}`

Returns a [switch object](/api/models#switch-model) containing a list of member objects.

### Update Switch

PATCH `/systems/{systemRef}/switches/{switchRef}`

JSON Body Parameters

|key|type|description|
|---|---|---|
|timestamp|datetime|when the switch started|

Returns a [switch object](/api/models#switch-model) containing a list of member objects on success.

### Update Switch Members

PATCH `/systems/{systemRef}/switches/{switchRef}/members`

Takes a list of member short IDs or UUIDs as input.

Returns a [switch object](/api/models#switch-model) containing a list of member objects on success.

### Delete Switch

DELETE `/systems/{systemRef}/switches/{switchRef}`

Returns 204 No Content on success.

---
## Misc

### Get Proxied Message Information

GET `/messages/{message}`

Message can be the ID of a proxied message, or the ID of the message that sent the proxy.

::: warning
Looking up messages by the original message ID only works **up to 30 minutes** after the message was sent.
:::

Returns a [message object](/api/models#message-object).
