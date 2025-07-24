#!/bin/bash

set -e

#tag=
#branch=
#push=

build() {
    bin=$1
    extra=$2

    f=$(mktemp)

    cat > $f << EOF
FROM alpine:latest
COPY .docker-bin/$bin /bin/$bin
$extra
CMD ["/bin/$bin"]
EOF

    echo "building $dockerfile"

    $dockerfile | docker build -t ghcr.io/pluralkit/$bin:$tag -f $f .

    rm $f

    if [ "$push" == "true" ]; then
      docker push ghcr.io/pluralkit/$bin:$tag
      docker image tag ghcr.io/pluralkit/$bin:$tag ghcr.io/pluralkit/$bin:$branch
      docker push ghcr.io/pluralkit/$bin:$branch
      if [ "$branch" == "main" ]; then
        docker image tag ghcr.io/pluralkit/$bin:$tag ghcr.io/pluralkit/$bin:latest
        docker push ghcr.io/pluralkit/$bin:latest
      fi
    fi
}

# add rust binaries here to build
build migrate
build api
build dispatch
build gateway
build avatars "COPY .docker-bin/avatar_cleanup /bin/avatar_cleanup"
build scheduled_tasks
build gdpr_worker
