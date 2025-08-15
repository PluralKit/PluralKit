use std::{
    env,
    error::Error,
    fs::{self, File},
    io::Write,
    path::Path,
};

fn main() -> Result<(), Box<dyn Error>> {
    let out_dir = env::var("OUT_DIR")?;
    let manifest_dir = env::var("CARGO_MANIFEST_DIR")?;
    let dest_path = Path::new(&out_dir).join("data.rs");
    let mut datafile = File::create(&dest_path)?;

    let prefix = manifest_dir + "/data";

    let ct = fs::read_dir("data/migrations")?
        .filter(|p| {
            p.as_ref()
                .unwrap()
                .file_name()
                .into_string()
                .unwrap()
                .contains(".sql")
        })
        .count();

    writeln!(&mut datafile, "const MIGRATIONS: [&'static str; {ct}] = [")?;
    for idx in 0..ct {
        writeln!(
            &mut datafile,
            "\tinclude_str!(\"{prefix}/migrations/{idx}.sql\"),"
        )?;
    }
    writeln!(&mut datafile, "];\n")?;

    writeln!(
        &mut datafile,
        "const CLEAN: &'static str = include_str!(\"{prefix}/clean.sql\");"
    )?;
    writeln!(
        &mut datafile,
        "const VIEWS: &'static str = include_str!(\"{prefix}/views.sql\");"
    )?;
    writeln!(
        &mut datafile,
        "const FUNCTIONS: &'static str = include_str!(\"{prefix}/functions.sql\");"
    )?;

    writeln!(
        &mut datafile,
        "const SEED: &'static str = include_str!(\"{prefix}/seed.sql\");"
    )?;

    Ok(())
}
