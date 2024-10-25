package main

import (
	"context"
	"fmt"
	"log"

	"golang.org/x/text/language"
	"golang.org/x/text/message"
)

var table_stat_keys = []string{"system", "member", "group", "switch"}

func plural(key string) string {
	if key[len(key)-1] == 'h' {
		return key + "es"
	}
	return key + "s"
}

func update_db_meta() {
	for _, key := range table_stat_keys {
		q := fmt.Sprintf("update info set %s_count = (select count(*) from %s)", key, plural(key))
		log.Println("data db query:", q)
		run_simple_pg_query(data_db, q)
	}

	data_stats := run_data_stats_query()
	for _, key := range table_stat_keys {
		val := data_stats[key+"_count"].(int64)
		log.Printf("%v: %v\n", key+"_count", val)
		do_stats_insert(plural(key), val)
	}
}

func update_db_message_meta() {
	count := get_message_count()

	_, err := data_db.Exec(context.Background(), "update info set message_count = $1", count)
	if err != nil {
		panic(err)
	}

	do_stats_insert("messages", int64(count))
}

func update_discord_stats() {
	redisStats := query_http_cache()

	guild_count := 0
	channel_count := 0

	for _, v := range redisStats {
		log.Println(v.GuildCount, v.ChannelCount)
		guild_count += v.GuildCount
		channel_count += v.ChannelCount
	}

	do_stats_insert("guilds", int64(guild_count))
	do_stats_insert("channels", int64(channel_count))

	if !set_guild_count {
		return
	}

	p := message.NewPrinter(language.English)
	s := p.Sprintf("%d", guild_count)

	cmd := rdb.Set(context.Background(), "pluralkit:botstatus", "in "+s+" servers", 0)
	if err := cmd.Err(); err != nil {
		panic(err)
	}
}
