package main

import (
	"context"

	redis "github.com/go-redis/redis/v8"
	"github.com/jackc/pgx/v4/pgxpool"
)

var data_db *pgxpool.Pool
var messages_db *pgxpool.Pool
var stats_db *pgxpool.Pool
var rdb *redis.Client

func run_simple_pg_query(c *pgxpool.Pool, sql string) {
	_, err := c.Exec(context.Background(), sql)
	if err != nil {
		panic(err)
	}
}

func connect_dbs() {
	data_db = pg_connect(get_env_var("DATA_DB_URI"))
	messages_db = pg_connect(get_env_var("MESSAGES_DB_URI"))
	stats_db = pg_connect(get_env_var("STATS_DB_URI"))
	rdb = redis_connect(get_env_var("REDIS_ADDR"))
}

func pg_connect(url string) *pgxpool.Pool {
	conn, err := pgxpool.Connect(context.Background(), url)
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
