---
title: Reference
permalink: /api
---

# API Reference

PluralKit has a basic HTTP REST API for querying and modifying your system.
The root endpoint of the API is `https://api.pluralkit.me/v2/`.

#### Authorization header token example
```
Authorization: z865MC7JNhLtZuSq1NXQYVe+FgZJHBfeBCXOPYYRwH4liDCDrsd7zdOuR45mX257
```

Endpoints will always return all fields, using `null` when a value is missing. On `PATCH` endpoints,
missing fields from the JSON request will be ignored and preserved as is, but on `POST` endpoints will
be set to `null` or cleared.

For models that have them, the keys `id`, `uuid` and `created` are **not** user-settable.

Endpoints taking JSON bodies (eg. most `PATCH` and `PUT` endpoints) require the `Content-Type: application/json` header set.

## User agent

The API requires the `User-Agent` header to be set to a non-empty string. Not doing so will return a `400 Bad Request` with a JSON body.

If you are developing an application exposed to the public, we would appreciate if your `User-Agent` uniquely identifies your application, and (if possible) provides some contact information for the developers - so that we are able to contact you if we notice your application doing something it shouldn't.

## Authentication

Authentication is done with a simple "system token". You can get your system token by running `pk;token` using the
Discord bot, either in a channel with the bot or in DMs. Then, pass this token in the `Authorization` HTTP header
on requests that require it. Failure to do so on endpoints that require authentication will return a `401 Unauthorized`.

Some endpoints show information that a given system may have set to private. If this is a specific field
(eg. description), the field will simply contain `null` rather than the true value. If this applies to entire endpoint
responses (eg. fronter, switches, member list), the entire request will return `403 Forbidden`. Authenticating with the
system's token (as described above) will override these privacy settings and show the full information. 

## Rate Limiting

To protect against abuse and manage server resources, PluralKit's API limits the amount of queries available. Currently, the following limits are applied:

- **10/second** for any `GET` requests other than the messages endpoint (`generic_get` scope)
- **10/second** for requests to the [Get Proxied Message Information](/api/endpoints/#get-proxied-message-information) endpoint (`message` scope)
- **3/second** for any `POST`, `PATCH`, or `DELETE` requests (`generic_update` scope)

We may raise the limits for individual users in a case-by-case basis; please ask [in the support server](https://discord.gg/PczBt78) if you need a higher limit.

::: tip
If you are looking to query a specific resource in your system repeatedly (polling), please consider using [Dispatch Webhooks](/api/dispatch) instead.
:::

The following rate limit headers are present on HTTP responses:

|name|description|
|---|---|
|X-RateLimit-Limit|The amount of total requests you have available per second.|
|X-RateLimit-Remaining|The amount of requests you have remaining until the next reset time.|
|X-RateLimit-Reset|The UNIX time (in milliseconds) when the ratelimit info will reset.|
|X-RateLimit-Scope|The type of rate limit the current request falls under.|

If you make more requests than you have available, the server will respond with a 429 status code and a JSON error body.

```json
{
  "message": "429: too many requests",
  "retry_after": 19, // the amount of milliseconds remaining until you can make more requests
  "code": 0
}
```

## Community API Libraries

The following API libraries have been created by members of our community. Please contact the developer of each library if you need support.

- **Python:** *PluralKit.py* ([PyPI](https://pypi.org/project/pluralkit/) | [Docs](https://pluralkit.readthedocs.io/en/latest/source/quickstart.html) | [Source code](https://github.com/almonds0166/pluralkit.py))
- **JavaScript:** *pkapi.js* ([npmjs](https://npmjs.com/package/pkapi.js) | [Docs](https://github.com/greysdawn/pk.js/wiki) | [Source code](https://github.com/greysdawn/pk.js))
- **Golang:** *pkgo* (install: `go get github.com/starshine-sys/pkgo/v2` | [Docs (godoc)](https://godocs.io/github.com/starshine-sys/pkgo/v2) | [Docs (pkg.go.dev)](https://pkg.go.dev/github.com/starshine-sys/pkgo/v2) | [Source code](https://github.com/starshine-sys/pkgo))
- **Kotlin:** *Plural.kt* ([Maven Repository](https://maven.proxyfox.dev/dev/proxyfox/pluralkt) | [Source code](https://github.com/The-ProxyFox-Group/Plural.kt))

Do let us know in the support server if you made a new library and would like to see it listed here!
