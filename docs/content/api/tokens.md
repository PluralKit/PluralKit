---
title: API keys / tokens
permalink: /api/tokens
---

# API keys / tokens

There are currently two types of API keys / tokens used by PluralKit - "legacy" tokens from the `pk;token` command (64 characters, a system can only have one valid token at a time); and "modern" API keys (variable length, always start with `pkapi:').

## "Legacy" tokens

"Legacy" PluralKit tokens look similar to the following:

```
LvWacQm3Yu+Jbhl8B7LR97Q4kfpAasTiB8/BY5/HJCppHFggzwOai6QBxehAJ53C
```

These tokens are supplied *as-is* in the `Authorization` HTTP header when talking to the PluralKit API (e.g. `Authorization: LvWacQm3Y...`)

Each PluralKit system can only have *one* valid "legacy" token at a time, and that token holds the keys to the entire castle - it grants full read/write privileges.

**PluralKit's API will stop accepting "legacy" tokens for authentication in the near future!** We do not yet have a deprecation plan set in stone, but there will be a significant notice period before this happens.

## "Modern" API keys

A "modern" PluralKit API key is made up of three components, separated by colons:

- The string `"pkapi"`
- A Base64-encoded JSON blob containing information about the API key
- An opaque signature

As an example:

```
pkapi:eyJ0aWQiOiI3NWEzODZlNy1mMjNlLTRmM2EtYjkwNC1jYTgwMzE0OWFmNWEiLCJzaWQiOiIyMmIwYjA3Yi00ZmE3LTRmYTEtYmYyNS1lZWI4NjY1ZjMyYzEiLCJ0eXBlIjoidXNlcl9jcmVhdGVkIiwic2NvcGVzIjpbIndyaXRlOmFsbCJdfQ==:nUjJPPtBOyPb1bYFhm24bU87N2Fb_oSaNnHEZkB-6ZSCSlAJvkyb32MTfmdEv3U6wNBlBQtQb0Fkv2nSvbNsCw
```

These tokens must be supplied with a "Bearer" prefix in the `Authorization` HTTP header when talking to the PluralKit API (e.g. `Authorization: Bearer pkapi:eyJ0aW...`).

The JSON blob in the above example API key contains the following:

```js
{
    // API key ID
    "tid": "75a386e7-f23e-4f3a-b904-ca803149af5a",

    // UUID of the PluralKit system the token belongs to
    "sid": "22b0b07b-4fa7-4fa1-bf25-eeb8665f32c1",  

    // "user_created" for manually generated API keys,
    // "external_app" for OAuth2 user API keys (coming soon!)
    "type": "user_created",

    // One or more scopes (see below)
    "scopes": ["write:all"]
}
```

### Scopes

In the below table, `<X>` refers to a *permission level* - one of the following:

- `publicread`: read-only access to *public* information
- `read`: read-only access to all (public *and* private) information
- `write`: read-write access to all information (implies `read`)

|scope|notes|
|---|---|
|`identify`|Read-only access to `/v2/systems/@me` - for proving the user providing the token has control of the PluralKit system|
|`<X>:system`|Access to core system data, system settings (including autoproxy), and server-specific settings|
|`<X>:members`|Access to member information, *not including group membership*|
|`<X>:groups`|Access to group information|
|`<X>:fronters`|Access to current system fronters|
|`<X>:switches`|Access to full system switch history (implies `<X>:fronters`)|
|`<X>:all`|Includes all other scopes|

### Issuing new API keys

TODO
