package main

import (
	"context"
	"log"
	"net/http"
	"net/http/httputil"
	"net/url"
	"time"
)

func proxyTo(host string) *httputil.ReverseProxy {
	rp := httputil.NewSingleHostReverseProxy(&url.URL{
		Scheme:   "http",
		Host:     host,
		RawQuery: "",
	})
	rp.ModifyResponse = logTimeElapsed
	return rp
}

// todo: this shouldn't be in this repo
var remotes = map[string]*httputil.ReverseProxy{
	"api.pluralkit.me":     proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:902]:5000"),
	"dash.pluralkit.me":    proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:902]:8080"),
	"sentry.pluralkit.me":  proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:902]:9000"),
	"grafana.pluralkit.me": proxyTo("[fdaa:0:ae33:a7b:8dd7:0:a:802]:3000"),
}

type ProxyHandler struct{}

func (p ProxyHandler) ServeHTTP(rw http.ResponseWriter, r *http.Request) {
	remote, ok := remotes[r.Host]
	if !ok {
		// unknown domains redirect to landing page
		http.Redirect(rw, r, "https://pluralkit.me", http.StatusFound)
		return
	}

	startTime := time.Now()
	r = r.WithContext(context.WithValue(r.Context(), "req-time", startTime))

	remote.ServeHTTP(rw, r)
}

func logTimeElapsed(resp *http.Response) error {
	r := resp.Request

	startTime := r.Context().Value("req-time").(time.Time)

	elapsed := time.Since(startTime)

	log.Printf("[%s] \"%s %s%s\" %d - %vms %s\n", r.Header.Get("Fly-Client-IP"), r.Method, r.Host, r.URL.Path, resp.StatusCode, elapsed.Milliseconds(), r.Header.Get("User-Agent"))

	return nil
}

func main() {
	http.ListenAndServe(":8080", ProxyHandler{})
}
