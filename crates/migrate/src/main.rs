#![feature(let_chains)]

use tracing::info;

include!(concat!(env!("OUT_DIR"), "/data.rs"));

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let db = libpk::db::init_data_db().await?;

    // clean
    // get current migration
    // migrate to latest
    // run views
    // run functions

    #[derive(sqlx::FromRow)]
    struct CurrentMigration {
        schema_version: i32,
    }

    let info = match sqlx::query_as("select schema_version from info")
        .fetch_optional(&db)
        .await
    {
        Ok(Some(result)) => result,
        Ok(None) => CurrentMigration { schema_version: -1 },
        Err(e) if format!("{e}").contains("relation \"info\" does not exist") => {
            CurrentMigration { schema_version: -1 }
        }
        Err(e) => return Err(e.into()),
    };

    info!("current migration: {}", info.schema_version);

    info!("running clean.sql");
    sqlx::raw_sql(fix_feff(CLEAN)).execute(&db).await?;

    for idx in (info.schema_version + 1) as usize..MIGRATIONS.len() {
        info!("running migration {idx}");
        sqlx::raw_sql(fix_feff(MIGRATIONS[idx as usize]))
            .execute(&db)
            .await?;
    }

    info!("running views.sql");
    sqlx::raw_sql(fix_feff(VIEWS)).execute(&db).await?;

    info!("running functions.sql");
    sqlx::raw_sql(fix_feff(FUNCTIONS)).execute(&db).await?;

    if let Ok(var) = std::env::var("SEED")
        && var == "true"
    {
        info!("running seed.sql");
        sqlx::raw_sql(fix_feff(SEED)).execute(&db).await?;
        info!(
            "example system created with hid 'exmpl', token 'vlPitT0tEgT++a450w1/afODy5NXdALcHDwryX6dOIZdGUGbZg+5IH3nrUsQihsw', guild_id 466707357099884544"
        );
    }

    info!("all done!");

    Ok(())
}

// some migration scripts have \u{feff} at the start
fn fix_feff(sql: &str) -> &str {
    sql.trim_start_matches("\u{feff}")
}
