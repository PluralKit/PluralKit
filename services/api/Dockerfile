FROM alpine:latest AS builder

WORKDIR /build

RUN apk add rustup build-base
# todo: arm64 target
RUN rustup-init --default-host x86_64-unknown-linux-musl --default-toolchain stable --profile default -y

COPY Cargo.toml /build/
COPY Cargo.lock /build/

# todo: fetch dependencies first to cache
# RUN cargo fetch

COPY lib/libpk /build/lib/libpk
COPY services/api/ /build/services/api

RUN source "$HOME/.cargo/env" && RUSTFLAGS='-C link-arg=-s' cargo build --bin api --release --target x86_64-unknown-linux-musl

RUN ls /build/target
RUN ls /build/target/release

FROM alpine:latest

COPY --from=builder /build/target/x86_64-unknown-linux-musl/release/api /bin/api

ENTRYPOINT [ "/bin/api" ]
