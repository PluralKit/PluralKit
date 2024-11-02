package main

import (
	"fmt"
	"log"
	"os"
	"runtime/debug"
	"strings"
	"time"
	"net/http"

	"github.com/getsentry/sentry-go"

    "github.com/prometheus/client_golang/prometheus"
    "github.com/prometheus/client_golang/prometheus/promauto"
    "github.com/prometheus/client_golang/prometheus/promhttp"
)

var set_guild_count = false

var (
    cleanupQueueLength = promauto.NewGauge(prometheus.GaugeOpts{
            Name: "pluralkit_image_cleanup_queue_length",
            Help: "Remaining image cleanup jobs",
    })
)

func main() {
	if _, ok := os.LookupEnv("SET_GUILD_COUNT"); ok {
		set_guild_count = true
	}

	err := sentry.Init(sentry.ClientOptions{
		Dsn: os.Getenv("SENTRY_DSN"),
	})
	if err != nil {
		panic(err)
	}

	log.Println("connecting to databases")
	connect_dbs()

	log.Println("starting scheduled tasks runner")
	go func() {
		wait_until_next_minute()

		go doforever(time.Minute, withtime("stats updater", update_db_meta))
		go doforever(time.Minute*10, withtime("message stats updater", update_db_message_meta))
		go doforever(time.Minute, withtime("discord stats updater", update_discord_stats))
		go doforever(time.Minute*30, withtime("queue deleted image cleanup job", queue_deleted_image_cleanup))
	}()

	go doforever(time.Second * 10, withtime("prometheus updater", update_prom))
	log.Println("listening for prometheus on :9000")
    http.Handle("/metrics", promhttp.Handler())
    http.ListenAndServe(":9000", nil)
}

func wait_until_next_minute() {
	now := time.Now().UTC().Add(time.Minute)
	after := time.Date(now.Year(), now.Month(), now.Day(), now.Hour(), now.Minute(), 0, 0, time.UTC)
	time.Sleep(after.Sub(time.Now().UTC()))
}

func get_env_var(key string) string {
	if val, ok := os.LookupEnv(key); ok {
		return val
	}
	panic(fmt.Errorf("missing `%s` in environment", key))
}

func withtime(name string, todo func()) func() {
	return func() {
		log.Println("running", name)
		timeBefore := time.Now()
		todo()
		timeAfter := time.Now()
		log.Println("ran", name, "in", timeAfter.Sub(timeBefore).String())
	}
}

func doforever(dur time.Duration, todo func()) {
	for {
		go wrapRecover(todo)
		time.Sleep(dur)
	}
}

func wrapRecover(todo func()) {
	defer func() {
		if err := recover(); err != nil {
			if val, ok := err.(error); ok {
				sentry.CaptureException(val)
			} else {
				sentry.CaptureMessage(fmt.Sprint("unknown error", err))
			}

			stack := strings.Split(string(debug.Stack()), "\n")
			stack = stack[7:]
			log.Printf("error running tasks: %v\n", err)
			fmt.Println(strings.Join(stack, "\n"))
		}
	}()

	todo()
}
