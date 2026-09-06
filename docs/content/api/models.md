---
title: Models
permalink: /api/models
---

# Models

A question mark (`?`) next to the *key name* means the key is optional - it may be omitted in API responses. A question mark next to the *key type* means the key is nullable - API responses may return `null` for that key, instead of the specified type.

In PATCH endpoints, all keys are optional. However, you must provide at least one valid key to update; please use a GET request if you want to query the existing information.
<br>Sending a PATCH request with an empty JSON object, or with a JSON object that contains no valid keys for the target entity, will result in a 400 bad request error.

Privacy objects (`privacy` key in models) contain values "private" or "public". Patching a privacy value to `null` will set to public. If you do not have access to view the privacy object of the member, the value of the `privacy` key will be null, rather than the values of individual privacy keys.

#### Notes on IDs

Every PluralKit entity has two IDs: a short (5 or 6 character) ID and a longer UUID. The short ID is unique across the resource (a member can have the same short ID as a system, for example), while the UUID is consistent for the lifetime of the entity and globally unique across the bot.

The PluralKit Discord bot can be configured to display short IDs in uppercase, or (in the case of 6-character IDs) as two parts of 3 characters separated by a dash (for example, `EXA-MPL`). For convenience, IDs are accepted by the API in any format displayable by the bot.

### System model

|key|type|notes|
|---|---|---|
|id|string||
|uuid|string||
|name|?string|100-character limit|
|description|?string|1000-character limit|
|tag|?string|79-character limit|
|pronouns|?string|100-character limit|
|avatar_url|?string|256-character limit, must be a publicly-accessible URL|
|banner|?string|256-character limit, must be a publicly-accessible URL|
|color|?string|6-character hex code, no `#` at the beginning|
|created|datetime||
|privacy|?system privacy object||

* System privacy keys: `name_privacy`, `description_privacy`, `avatar_privacy`, `banner_privacy`, `pronoun_privacy`, `member_list_privacy`, `group_list_privacy`, `front_privacy`, `front_history_privacy`

### Member model

|key|type|notes|
|---|---|---|
|id|string||
|uuid|string||
|?system|string|id of system this member is registered in (only returned in `/members/:id` endpoint)|
|name|string|100-character limit|
|display_name|?string|100-character limit|
|color|?string|6-character hex code, no `#` at the beginning|
|birthday|?string|`YYYY-MM-DD` format, 0004 hides the year|
|pronouns|?string|100-character-limit|
|avatar_url|?string|256-character limit, must be a publicly-accessible URL|
|webhook_avatar_url|?string|256-character limit, must be a publicly-accessible URL|
|banner|?string|256-character limit, must be a publicly-accessible URL|
|description|?string|1000-character limit|
|created|?datetime||
|proxy_tags|array of [ProxyTag objects](#proxytag-object)|
|keep_proxy|boolean||
|tts|boolean||
|autoproxy_enabled|?boolean||
|message_count|?int||
|last_message_timestamp|?datetime||
|privacy|?member privacy object||

* Member privacy keys: `visibility`, `name_privacy`, `description_privacy`, `birthday_privacy`, `pronoun_privacy`, `avatar_privacy`, `banner_privacy`, `metadata_privacy`, `proxy_privacy`

#### ProxyTag object

| Key    | Type    |
| ------ | ------- |
| prefix | ?string |
| suffix | ?string |

* Note: `prefix + "text" + suffix` must be shorter than 100 characters in total.

### Group model

|key|type|notes|
|---|---|---|
|id|string||
|uuid|string||
|?system|string|id of system this group is registered in (only returned in `/groups/:id` endpoint)|
|name|string|100-character limit|
|display_name|?string|100-character limit|
|description|?string|1000-character limit|
|created|?datetime||
|icon|?string|256-character limit, must be a publicly-accessible URL|
|banner|?string|256-character limit, must be a publicly-accessible URL|
|color|?string|6-character hex code, no `#` at the beginning|
|privacy|?group privacy object||

* Group privacy keys: `name_privacy`, `description_privacy`, `banner_privacy`, `icon_privacy`, `list_privacy`, `metadata_privacy`, `visibility`

### Switch model

|key|type|notes|
|---|---|---|
|id|uuid||
|timestamp|datetime||
| members   | list of id/Member | Is sometimes in plain ID list form (eg. `GET /systems/:id/switches`), sometimes includes the full Member model (eg. `GET /systems/:id/fronters`). |

### Message model

|key|type|notes|
|---|---|---|
|timestamp|datetime||
|id|snowflake|The ID of the message sent by the webhook. Encoded as string for precision reasons.|
|original|snowflake|The ID of the (now-deleted) message that triggered the proxy. Encoded as string for precision reasons.|
|sender|snowflake|The user ID of the account that triggered the proxy. Encoded as string for precision reasons.|
|channel|snowflake|The ID of the channel the message was sent in. Encoded as string for precision reasons.|
|guild|snowflake|The ID of the server the message was sent in. Encoded as string for precision reasons.|
|system?|full System object|The system that proxied the message. Null if the member associated with this message was deleted.|
|member?|full Member object|The member that proxied the message. Null if the member associated with this message was deleted.|

### System settings model

|key|type|notes|
|---|---|---|
|timezone|string|defaults to `UTC`|
|pings_enabled|boolean|whether proxied messages can be pinged using the :bell: reaction|
|latch_timeout|int?|seconds after which latch autoproxy will timeout (null/default is 6 hours, 0 is "never")|
|member_default_private*|boolean|whether members created through the bot have privacy settings set to private by default|
|group_default_private*|boolean|whether groups created through the bot have privacy settings set to private by default|
|show_private_info|boolean|whether the bot shows the system's own private information without a `-private` flag|
|member_limit|int|read-only, defaults to 1000|
|group_limit|int|read-only, defaults to 250|
|case_sensitive_proxy_tags|bool|whether the bot will match proxy tags matching only the case used in the trigger message|
|proxy_error_message_enabled|bool|whether the bot will show errors when proxying fails|
|hid_display_split|bool|whether 6-character ids will be shown by the bot as two 3-character parts separated by a `-`|
|hid_display_caps|bool|whether ids will be shown by the bot in uppercase|
|hid_list_padding|[ID padding format](#id-padding-format-enum)|whether the bot will pad 5-character ids in lists|
|proxy_switch|[proxy switch action](#proxy-switch-action-enum)|switch action the bot will take when proxying|
|name_format|string|format used for webhook names during proxying (defaults to `{name} {tag}`)|

\* this *does not* affect members/groups created through the API - please specify privacy keys in the JSON payload instead

#### ID padding format enum

|key|description|
|---|---|
|off|do not pad 5-character ids|
|left|add a padding space to the left of 5-character ids in lists|
|right|add a padding space to the right of 5-character ids in lists|

#### Proxy switch action enum

|key|description|
|---|---|
|off|do nothing|
|new|if the currently proxied member is not present in the current switch, log a new switch with this member|
|add|if the current switch has 0 members, log a new switch with the currently proxied member; otherwise, add the member to the current switch|

### Public system settings model

This model is used when querying system settings without authenticating, or for a system other than the one you are authenticated as.

|key|type|notes|
|---|---|---|
|pings_enabled|boolean|whether proxied messages can be pinged using the :bell: reaction|
|latch_timeout|int?|seconds after which latch autoproxy will timeout (null/default is 6 hours, 0 is "never")|
|case_sensitive_proxy_tags|bool|whether the bot will match proxy tags matching only the case used in the trigger message|
|proxy_error_message_enabled|bool|whether the bot will show errors when proxying fails|
|hid_display_split|bool|whether 6-character ids will be shown by the bot as two 3-character parts separated by a `-`|
|hid_display_caps|bool|whether ids will be shown by the bot in uppercase|
|hid_list_padding|[ID padding format](#id-padding-format-enum)|whether the bot will pad 5-character ids in lists|
|proxy_switch|[proxy switch action](#proxy-switch-action-enum)|switch action the bot will take when proxying|
|name_format|string|format used for webhook names during proxying (defaults to `{name} {tag}`)|

### System guild settings model

|key|type|notes|
|---|---|---|
|?guild_id|snowflake|only sent if the guild ID isn't already known (in dispatch payloads)|
|proxying_enabled|boolean||
|tag|?string|79-character limit|
|tag_enabled|boolean||
|avatar_url|?string|256-character limit|
|display_name|?string|100-character limit|


### Autoproxy settings model
|key|type|notes|
|---|---|---|
|?guild_id|snowflake|only sent if the guild ID isn't already known (in dispatch payloads)|
|?channel_id|snowflake|only sent if the channel ID isn't already known (in dispatch payloads)|
|autoproxy_mode|[autoproxy mode](#autoproxy-mode-enum)||
|autoproxy_member|?member id|must be `null` if autoproxy_mode is set to `front`|
|last_latch_timestamp|?datetime|read-only|

#### Autoproxy mode enum

|key|description|
|---|---|
|off|autoproxy is disabled|
|front|autoproxy is set to the first member in the current fronters list, or disabled if the current switch contains no members|
|latch|autoproxy is set to the last member who sent a proxied message in the server|
|member|autoproxy is set to a specific member (see `autoproxy_member` key)|

### Member guild settings model

|key|type|notes|
|---|---|---|
|?guild_id|snowflake|only sent if the guild ID isn't already known (in dispatch payloads)|
|display_name|?string|100-character limit|
|avatar_url|?string|256-character limit, must be a publicly-accessible URL|
|keep_proxy|?boolean||
