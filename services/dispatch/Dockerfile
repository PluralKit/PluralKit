FROM alpine:latest AS builder

WORKDIR /build

RUN apk add rustup build-base
RUN rustup-init --default-host x86_64-unknown-linux-musl --default-toolchain nightly-2024-08-20 --profile default -y

ENV PATH=/root/.cargo/bin:$PATH
ENV RUSTFLAGS='-C link-arg=-s'

COPY . .

RUN cargo build --bin dispatch --release --target x86_64-unknown-linux-musl

FROM alpine:latest
COPY --from=builder /build/target/x86_64-unknown-linux-musl/release/dispatch /usr/local/bin/dispatch
ENTRYPOINT ["/usr/local/bin/dispatch"]
