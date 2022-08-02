# twilight requires newer rustc than what is in alpine:latest
FROM alpine:edge AS builder

RUN apk add cargo protobuf

# Precache crates.io index
RUN cargo search >/dev/null

WORKDIR /build
COPY proto/ /build/proto
COPY gateway/ /build/gateway
COPY myriad_rs/ /build/myriad_rs

# todo: cache build of myriad_rs elsewhere

RUN (cd gateway && cargo build --release)

FROM alpine:latest

COPY --from=builder /build/gateway/target/release/pluralkit /opt/gateway

ENTRYPOINT ["/opt/gateway"]
