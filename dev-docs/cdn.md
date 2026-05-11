## PluralKit CDN / avatars service

CDN needs:
- s3 bucket for image storage
- s3 bucket for uploads, 1h ttl policy
- s3 credentials for api service (RW on both buckets)
- s3 credentials for avatars service (RW only on uploads bucket)
    - for local development this can just be the same as api service credentials
- reverse proxy -> tbd
