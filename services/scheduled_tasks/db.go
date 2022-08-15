package main

import (
	"context"

	redis "github.com/go-redis/redis/v8"
	pgx "github.com/jackc/pgx/v4"
)

var data_db *pgx.Conn
var stats_db *pgx.Conn
var rdb *redis.Client

func run_simple_pg_query(c *pgx.Conn, sql string) {
	_, err := c.Exec(context.Background(), sql)
	if err != nil {
		panic(err)
	}
}

func connect_dbs() {
	data_db = pg_connect(get_env_var("DATA_DB_URI"))
	stats_db = pg_connect(get_env_var("STATS_DB_URI"))
	rdb = redis_connect(get_env_var("REDIS_ADDR"))
}

func pg_connect(url string) *pgx.Conn {
	conn, err := pgx.Connect(context.Background(), url)
	if err != nil {
		panic(err)
	}

	return conn
}

func redis_connect(url string) *redis.Client {
	return redis.NewClient(&redis.Options{
		Addr: url,
		DB:   0,
	})
}
