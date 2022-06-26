FROM alpine:latest AS builder

RUN apk add go

WORKDIR /build

COPY scheduled_tasks/ /build

RUN go build .

FROM alpine:latest

COPY --from=builder /build/scheduled_tasks /bin/runner

ENTRYPOINT ["/bin/runner"]
