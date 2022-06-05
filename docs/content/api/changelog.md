---
title: Changelog
permalink: /api/changelog
---

# Version history

* 2022-01-11
  * Member / system keys in message object are now nullable.
* 2021-11-07 (v2)
  * API v2 has been released. API v1 is now deprecated.
* 2020-07-28
  * The unversioned API endpoints have been removed.
* 2020-06-17 (v1.1)
  * The API now has values for granular member privacy. The new fields are as follows: `visibility`, `name_privacy`, `description_privacy`, `avatar_privacy`, `birthday_privacy`, `pronoun_privacy`, `metadata_privacy`. All are strings and accept the values of `public`, `private` and `null`.
  * The `privacy` field has now been deprecated and should not be used. It's still returned (mirroring the `visibility` field), and writing to it will write to *all privacy options*.
* 2020-05-07
  * The API (v1) is now formally(ish) defined with OpenAPI v3.0. [The definition file can be found here.](https://github.com/PluralKit/PluralKit/blob/master/PluralKit.API/openapi.yaml)
* 2020-02-10
  * Birthdates with no year can now be stored using `0004` as a year, for better leap year support. Both options remain valid and either may be returned by the API.
  * Added privacy set/get support, meaning you will now see privacy values in authed requests and can set them.
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
