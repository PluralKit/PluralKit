package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"io"
	"os"
	"net/http"
	"strconv"
)

type httpstats struct {
	Up bool `json:"up"`
	GuildCount   int `json:"guild_count"`
	ChannelCount int `json:"channel_count"`
}

func query_http_cache() []httpstats {
	var values []httpstats

	http_cache_url := os.Getenv("HTTP_CACHE_URL")
	if http_cache_url == "" {
		panic("missing HTTP_CACHE_URL in environment")
	}

	cluster_count, err := strconv.Atoi(os.Getenv("CLUSTER_COUNT"))
	if err != nil {
		panic(fmt.Sprintf("missing or invalid CLUSTER_COUNT in environment"))
	}

	for i := range cluster_count {
		log.Printf("querying gateway cluster %v for discord stats\n", i)
		url := fmt.Sprintf("http://cluster%v.%s:5000/stats", i, http_cache_url)
		resp, err := http.Get(url)
		if err != nil {
			panic(err)
		}
		defer resp.Body.Close()
		if resp.StatusCode != http.StatusFound {
			panic(fmt.Sprintf("got status %v trying to query %v.%s:5000", resp.Status, i, http_cache_url))
		}
		var s httpstats
		data, err := io.ReadAll(resp.Body)
		if err != nil {
			panic(err)
		}
		err = json.Unmarshal(data, &s)
		if err != nil {
			panic(err)
		}
		if s.Up == false {
			panic("gateway is not up yet, skipping stats collection")
		}
		values = append(values, s)
	}

	return values
}

type rstatval struct {
	GuildCount   int `json:"GuildCount"`
	ChannelCount int `json:"ChannelCount"`
}

func run_redis_query() []rstatval {
	cmd := rdb.HGetAll(context.Background(), "pluralkit:cluster_stats")
	if err := cmd.Err(); err != nil {
		panic(err)
	}

	res, err := cmd.Result()
	if err != nil {
		panic(err)
	}

	var values []rstatval

	for _, data := range res {
		var tmp rstatval
		if err = json.Unmarshal([]byte(data), &tmp); err != nil {
			panic(err)
		}

		values = append(values, tmp)
	}

	return values
}

func get_message_count() int {
	var count int
	row := messages_db.QueryRow(context.Background(), "select count(*) as count from messages")
	if err := row.Scan(&count); err != nil {
		panic(err)
	}
	return count
}

func get_image_cleanup_queue_length() int {
	var count int
	row := data_db.QueryRow(context.Background(), "select count(*) as count from image_cleanup_jobs")
	if err := row.Scan(&count); err != nil {
		panic(err)
	}
	return count
}

func run_data_stats_query() map[string]interface{} {
	s := map[string]interface{}{}

	rows, err := data_db.Query(context.Background(), "select * from info")
	if err != nil {
		panic(err)
	}
	descs := rows.FieldDescriptions()

	for rows.Next() {
		for i, column := range descs {
			if string(column.Name) == "message_count" {
				continue
			}
			values, err := rows.Values()
			if err != nil {
				panic(err)
			}

			s[string(column.Name)] = values[i]
		}
	}

	return s
}

func do_stats_insert(table string, value int64) {
	if stats_db == nil {
		return
	}

	sql := fmt.Sprintf("insert into %s values (now(), $1)", table)
	log.Println("stats db query:", sql, "value:", value)
	_, err := stats_db.Exec(context.Background(), sql, value)
	if err != nil {
		panic(err)
	}
}
