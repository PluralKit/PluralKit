#!/usr/bin/env python3


import os, json, subprocess

dispatch_data = os.environ.get("DISPATCH_DATA")

def must_get_env(name):
    val = os.environ.get(name)
    if val == "":
        raise "meow"
    return val

def docker_build(data):
    # file="", tags=[], root="/"
    pass

def create_jobs():
    modify_regexes = {
        "^ci/": "all",

        "^docs/": "bin_docs",
        "^dashboard/": "bin_dashboard",

        "\.rs$": "format_rs",
        "\.cs$": "format_cs",

        "^Cargo.lock": "all_rs",

        "^services/api": "bin_api",
        # dispatch doesn't use libpk
        "^services/dispatch": "bin_dispatch",
        "^services/scheduled_tasks": "bin_scheduled_tasks",

        # one image for all dotnet
        "^PluralKit\.": "bin_dotnet",
        "^Myriad": "bin_dotnet",
    }

    aliases = {
        "all": ["bin_dotnet", "bin_api", "bin_dispatch", "bin_scheduled_tasks", "bin_dashboard"],
        "all_rs": ["bin_api", "bin_dispatch"],
    }

    now = must_get_env("CUR_SHA")
    before = must_get_env("OLD_SHA")
    changed_files = subprocess.check_output(["git", "diff", "--name-only", before, now])

    jobs = set([])
    for key in modify_regexes.keys():
        if true:
            jobs = jobs | modify_regexes[key]

    for key in changes:
        if aliases.get(key) is not None:
            jobs = jobs | aliases[key]
            jobs = jobs - [key]

    pass

if __name__ == "__main__":
    print("hello from python!")
    subprocess.run(["docker", "run", "--rm", "-it", "hello-world"], check=True)

    return
    return create_jobs() if dispatch_data == ""

    data = json.loads(dispatch_data)
    match data.get("action"):
        case "docker_build":
            return docker_build(data.get("data"))
        case "rustfmt":
        case "dotnet_format":
        case _:
            print (f"data unknown: {dispatch_data}")
            os.exit(1)
