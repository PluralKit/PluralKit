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

## Authentication
Authentication is done with a simple "system token". You can get your system token by running `pk;token` using the
Discord bot, either in a channel with the bot or in DMs. Then, pass this token in the `Authorization` HTTP header
on requests that require it. Failure to do so on endpoints that require authentication will return a `401 Unauthorized`.

Some endpoints show information that a given system may have set to private. If this is a specific field
(eg. description), the field will simply contain `null` rather than the true value. If this applies to entire endpoint
responses (eg. fronter, switches, member list), the entire request will return `403 Forbidden`. Authenticating with the
system's token (as described above) will override these privacy settings and show the full information. 

## Rate Limiting

By default, there is a per-IP limit of 2 requests per second across the API. If you exceed this limit, you will get a 429 response code and will have to try again later.

## Community API Libraries

The following API libraries have been created by members of our community. Please contact the developer of each library if you need support.

- **Python:** *PluralKit.py* ([PyPI](https://pypi.org/project/pluralkit/) | [Docs](https://pluralkit.readthedocs.io/en/latest/source/quickstart.html) | [Source code](https://github.com/almonds0166/pluralkit.py))
- **JavaScript:** *pkapi.js* ([npmjs](https://npmjs.com/package/pkapi.js) | [Docs](https://github.com/greysdawn/pk.js/wiki) | [Source code](https://github.com/greysdawn/pk.js))
- **Golang:** *pkgo* (install: `go get github.com/starshine-sys/pkgo` | [Docs (godoc)](https://godocs.io/github.com/starshine-sys/pkgo) | [Docs (pkg.go.dev)](https://pkg.go.dev/github.com/starshine-sys/pkgo) | [Source code](https://github.com/starshine-sys/pkgo))

Do let us know in the support server if you made a new library and would like to see it listed here!
