#!/usr/bin/env python3

import os, sys, json, subprocess, random, time, re
import urllib.request

global_fail = False

def must_get_env(name):
    val = os.environ.get(name)
    if val == "":
        raise "meow"
    return val

def docker_build(data):
    # file="", tags=[], root="/"
    pass

def spawn_job(name):
    req = urllib.request.Request(
        f"https://api.github.com/repos/pluralkit/pluralkit/actions/workflows/ci-runner.yml/dispatches",
        method='POST',
        headers={
            'Accept': 'application/vnd.github+json',
            'Authorization': f'Bearer {must_get_env("GITHUB_APP_TOKEN")}',
            'content-type':'application/json'
        },
        data=bytes(json.dumps({
            'ref': 'refs/heads/new-ci',
            'inputs': {
                'dispatchData': json.dumps({
                   'action': name,
                   'sha': must_get_env("GIT_SHA"),
                })
            }
        }), 'UTF-8')
    )

    try:
        with urllib.request.urlopen(req) as response:
            response_code = response.getcode()
            response_data = response.read()
            print(f"{response_code} spawned job {name}: {response_data}")
    except urllib.error.HTTPError as e:
        response_code = e.getcode()
        response_data = e.read()
        print(f"{response_code} failed to spawn job {name}: {response_data}")
        global global_fail
        global_fail = True

def create_jobs():
    modify_regexes = {
        r'^ci/': "all",

        r'^docs/': "bin_docs",
        r'^dashboard/': "bin_dashboard",

        r'\.rs$': "format_rs",
        r'\.cs$': "format_cs",

        r'^Cargo.lock': "all_rs",

        r'^services/api': "bin_api",
        # dispatch doesn't use libpk
        r'^services/dispatch': "bin_dispatch",
        r'^services/scheduled_tasks': "bin_scheduled_tasks",

        # one image for all dotnet
        r'^PluralKit\.': "bin_dotnet",
        r'^Myriad': "bin_dotnet",
    }

    aliases = {
        "all": ["bin_dotnet", "bin_api", "bin_dispatch", "bin_scheduled_tasks", "bin_dashboard"],
        "all_rs": ["bin_api", "bin_dispatch"],
    }

    subprocess.run(["git", "clone", must_get_env("REPO_URL")])
    os.chdir(os.path.basename(must_get_env("REPO_URL")))

    now = must_get_env("GIT_SHA")
    before = must_get_env("OLD_SHA")
    changed_files = subprocess.check_output(["git", "diff", "--name-only", before, now])

    jobs = set([])
    if must_get_env("IS_FORCE_PUSH") == "true":
        jobs = jobs | aliases["all"]
        jobs = jobs | ["format_cs", "format_rs"]
    else:
        for key in modify_regexes.keys():
            if re.match(key, str(changed_files), flags=re.MULTILINE) is not None:
                jobs = jobs | set(modify_regexes[key])

        for key in jobs:
            if aliases.get(key) is not None:
                jobs = jobs | set(aliases[key])
                jobs = jobs - set([key])

    # test
    jobs = jobs | set(["test"])

    # do this in a tx or something
    for job in jobs:
        spawn_job(job)

    if len(jobs) == 0:
        print("no jobs to run (??)")

    global global_fail
    return 1 if global_fail else 0

if __name__ == "__main__":
    print("hello from python!")

    sys.exit(create_jobs())
