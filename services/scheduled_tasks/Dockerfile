FROM alpine:latest AS builder

RUN apk add go

WORKDIR /build

COPY ./ /build

RUN GOTOOLCHAIN=auto go build .

FROM alpine:latest

COPY --from=builder /build/scheduled_tasks /bin/runner

ENTRYPOINT ["/bin/runner"]
