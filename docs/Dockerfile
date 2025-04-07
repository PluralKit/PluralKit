FROM alpine:latest AS builder

RUN apk add nodejs-current yarn

WORKDIR /build
COPY . .
RUN yarn
ENV NODE_OPTIONS=--openssl-legacy-provider
RUN yarn build

FROM alpine:latest

RUN apk add caddy

WORKDIR /app
COPY --from=builder /build/content/.vuepress/dist /app

ENTRYPOINT [ "/usr/sbin/caddy", "file-server", "-l", "0.0.0.0:8000", "-r", "/app" ]
