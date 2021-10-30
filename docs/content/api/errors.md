---
title: Errors and Status Codes
permalink: /api/errors
---

# Errors and Status Codes

When something goes wrong, the API will send back a 4xx HTTP status code, along with a JSON object describing the error. 

### Error Response Model

|key|type|description|
|---|---|---|
|code|int|numerical error code|
|message|string|description of the error|
|?errors|map of entity keys to list of error objects*|details on the error|
|?retry_after|int|if this is a rate limit error, the number of milliseconds after which you can retry the request|

* Only returned for model parsing errors.

### Error Object

|key|type|description|
|---|---|---|
|message|string|error description|
|?max_length|int|if this is an error indicating a key is too long, the maximum allowed length for the key|
|?actual_length|int|if this is an error indicating a key is too long, the length of the provided value|

## JSON error codes

|code|HTTP response code|meaning|
|---|---|---|
|0|500|Internal server error, try again later|
|0|400|Bad Request (usually invalid JSON)|
|0|401|Missing or invalid Authorization header|
|20001|404|System not found.|
|20002|404|Member not found.|
|20003|404|Member '{memberRef}' not found.|
|20004|404|Group not found.|
|20005|404|Group '{groupRef}' not found.|
|20006|404|Message not found.|
|20007|404|Switch not found.|
|20008|404|Switch not found, switch associated with different system, or unauthorized to view front history.|
|20009|404|No system guild settings found for target guild.|
|20010|404|No member guild settings found for target guild.|
|30001|403|Unauthorized to view member list|
|30002|403|Unauthorized to view group list|
|30003|403|Unauthorized to view group member list|
|30004|403|Unauthorized to view current fronters.|
|30005|403|Unauthorized to view front history.|
|30006|403|Target member is not part of your system.|
|30007|403|Target group is not part of your system.|
|30008|403|Member '{memberRef}' is not part of your system.|
|30009|403|Group '{groupRef}' is not part of your system.|
|40002|400|Missing autoproxy member for member-mode autoproxy.|
|40003|400|Duplicate members in member list.|
|40004|400|Member list identical to current fronter list.|
|40005|400|Switch with provided timestamp already exists.|
|40006|400|Invalid switch ID.|
