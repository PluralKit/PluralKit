#!/usr/bin/env python3

import os, sys, json, subprocess, random, time, datetime
import urllib.request

def must_get_env(name):
    val = os.environ.get(name)
    if val == "":
        raise "meow"
    return val

def docker_build(data):
    # file="", tags=[], root="/"
    pass

def take_some_time():
    time.sleep(random.random() * 10)

def report_status(name, start_time, exit=None):
    status=""
    match exit:
        case None:
            status = "in_progress"
        case True:
            status = "success"
        case False:
            status = "failure"

    data = {
        'name': name,
        'head_sha': must_get_env("GIT_SHA"),
        'status': status,
        'started_at': start_time,
        'output': {
            'title': name,
            'summary': f"Check logs at {must_get_env("ACTION_LOGS_URL")}",
            'text': "[]",
            'annotations': []
        },
    }

    if exit is not None:
        data['completed_at'] = datetime.datetime.now(tz=datetime.timezone.utc).isoformat(timespec='seconds')

    req = urllib.request.Request(
        f"https://api.github.com/repos/pluralkit/pluralkit/check-runs",
        method='POST',
        headers={
            'Accept': 'application/vnd.github+json',
            'Authorization': f'Bearer {must_get_env("GITHUB_APP_TOKEN")}',
            'content-type':'application/json'
        },
        data=json.dumps(data)
    )

    try:
        with urllib.request.urlopen(request) as response:
            response_code = response.getcode()
            response_data = response.read()
            print(f"{response_code} updated status {data}: {response_data}")
    except urllib.error.HTTPError as e:
        response_code = e.getcode()
        response_data = e.read()
        print(f"{response_code} failed to update status {name}: {response_data}")

def run_job(data):
    subprocess.check_output(["git", "clone", must_get_env("REPO_URL")])
    os.chdir(os.path.basename(must_get_env("REPO_URL")))
    subprocess.run(["git", "checkout", must_get_env("GIT_SHA")])
    
    # run actual job
    take_some_time()

def main():
    print("hello from python!")

    dispatch_data = os.environ.get("DISPATCH_DATA")
    if dispatch_data == "":
        print("no data!")
        return 1

    data = json.loads(dispatch_data)
    print("running {dispatch_data}")

    time_started = datetime.datetime.now(tz=datetime.timezone.utc).isoformat(timespec='seconds')
    report_status(data["action"], time_started)

    ok = True
    try:
        run_job(data)
    except Exception:
        ok = False
        print("job failed!")
        traceback.format_exc()

    report_status(data["action"], time_started, ok)

    return 0 if ok else 1

if __name__ == "__main__":
    sys.exit(main())