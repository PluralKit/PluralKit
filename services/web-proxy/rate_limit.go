package main

import (
	"fmt"
	"net/http"
	"time"

	"web-proxy/redis_rate"
)

func is_api_ratelimited(rw http.ResponseWriter, r *http.Request) bool {
	var limit int
	var key string

	if r.Header.Get("X-PluralKit-App") == token2 {
		limit = 20
		key = "token2"
	} else {
		limit = 2
		key = r.Header.Get("Fly-Client-IP")
	}

	res, err := limiter.Allow(r.Context(), "ratelimit:"+key, redis_rate.Limit{
		Period: time.Second,
		Rate:   limit,
		Burst:  5,
	})
	if err != nil {
		panic(err)
	}

	rw.Header().Set("X-RateLimit-Limit", fmt.Sprint(limit))
	rw.Header().Set("X-RateLimit-Remaining", fmt.Sprint(res.Remaining))
	rw.Header().Set("X-RateLimit-Reset", fmt.Sprint(time.Now().Add(res.ResetAfter).UnixNano()/1_000_000))

	if res.Allowed < 1 {
		rw.WriteHeader(429)
		rw.Write([]byte(`{"message":"429: too many requests","retry_after":` + fmt.Sprint(res.RetryAfter.Milliseconds()) + `,"code":0}`))
		return true
	}

	return false
}
