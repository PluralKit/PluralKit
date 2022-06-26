package main

import (
	"fmt"
	"log"
	"os"
	"time"
)

func main() {
	log.Println("connecting to databases")
	connect_dbs()

	log.Println("starting scheduled tasks runner")
	wait_until_next_minute()
	go doforever(time.Minute*10, withtime("message stats updater", update_db_message_meta))
	doforever(time.Minute, withtime("scheduled tasks", task_main))
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
		timeBefore := time.Now()
		todo()
		timeAfter := time.Now()
		log.Println("ran", name, "in", timeAfter.Sub(timeBefore).String())
	}
}

func doforever(dur time.Duration, todo func()) {
	for {
		go todo()
		time.Sleep(dur)
	}
}
