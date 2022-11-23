package main

import (
	"context"
	"log"
	"net/http"
	"net/http/httputil"
	"strconv"
	"strings"
	"time"

	"github.com/go-redis/redis/v8"
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promhttp"

	"web-proxy/redis_rate"
)

var limiter *redis_rate.Limiter

// todo: be able to raise ratelimits for >1 consumers
var token2 string

var remotes = map[string]*httputil.ReverseProxy{
	"api.pluralkit.me":       proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:2]:8080"),
	"dash.pluralkit.me":      proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:2]:8080"),
	"sentry.pluralkit.me":    proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:202]:9000"),
	"plausible.pluralkit.me": proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:202]:8000"),
}

func init() {
	redisHost := requireEnv("REDIS_HOST")
	redisPassword := requireEnv("REDIS_PASSWORD")

	rdb := redis.NewClient(&redis.Options{
		Addr:     redisHost,
		Username: "default",
		Password: redisPassword,
	})
	limiter = redis_rate.NewLimiter(rdb)

	token2 = requireEnv("TOKEN2")

	remotes["dash.pluralkit.me"].ModifyResponse = modifyDashResponse
}

type ProxyHandler struct{}

func (p ProxyHandler) ServeHTTP(rw http.ResponseWriter, r *http.Request) {
	remote, ok := remotes[r.Host]
	if !ok {
		// unknown domains redirect to landing page
		http.Redirect(rw, r, "https://pluralkit.me", http.StatusFound)
		return
	}

	if r.Host == "api.pluralkit.me" {
		// root
		if r.URL.Path == "" {
			// api root path redirects to docs
			http.Redirect(rw, r, "https://pluralkit.me/api/", http.StatusFound)
			return
		}

		// CORS headers
		rw.Header().Add("Access-Control-Allow-Origin", "*")
		rw.Header().Add("Access-Control-Allow-Methods", "*")
		rw.Header().Add("Access-Control-Allow-Credentials", "true")
		rw.Header().Add("Access-Control-Allow-Headers", "Content-Type, Authorization, sentry-trace")
		rw.Header().Add("Access-Control-Max-Age", "86400")

		if r.Method == http.MethodOptions {
			rw.WriteHeader(200)
			return
		}

		if is_api_ratelimited(rw, r) {
			return
		}
	}

	startTime := time.Now()
	r = r.WithContext(context.WithValue(r.Context(), "req-time", startTime))

	remote.ServeHTTP(rw, r)
}

func logTimeElapsed(resp *http.Response) error {
	r := resp.Request

	startTime := r.Context().Value("req-time").(time.Time)

	elapsed := time.Since(startTime)
	metric.With(map[string]string{
		"domain": r.Host,
		"method": r.Method,
		"status": strconv.Itoa(resp.StatusCode),
		"route":  cleanPath(r.Host, r.URL.Path),
	}).Observe(elapsed.Seconds())

	log.Printf("[%s] \"%s %s%s\" %d - %vms %s\n", r.Header.Get("Fly-Client-IP"), r.Method, r.Host, r.URL.Path, resp.StatusCode, elapsed.Milliseconds(), r.Header.Get("User-Agent"))

	return nil
}

func modifyDashResponse(resp *http.Response) error {
	r := resp.Request

	// cache built+hashed dashboard js/css files forever
	is_dash_static_asset := strings.HasPrefix(r.URL.Path, "/assets/") &&
		(strings.HasSuffix(r.URL.Path, ".js") || strings.HasSuffix(r.URL.Path, ".css") || strings.HasSuffix(r.URL.Path, ".map"))

	if is_dash_static_asset && resp.StatusCode == 200 {
		resp.Header.Add("Cache-Control", "max-age: 604800")
	}

	return logTimeElapsed(resp)
}

func main() {
	prometheus.MustRegister(metric)

	http.Handle("/metrics", promhttp.Handler())
	go http.ListenAndServe(":9091", nil)

	http.ListenAndServe(":8080", ProxyHandler{})
}
