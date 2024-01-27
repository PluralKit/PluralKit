package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
)

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

func run_data_stats_query() map[string]interface{} {
	s := map[string]interface{}{}

	rows, err := data_db.Query(context.Background(), "select * from info")
	if err != nil {
		panic(err)
	}
	descs := rows.FieldDescriptions()

	for rows.Next() {
		for i, column := range descs {
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
