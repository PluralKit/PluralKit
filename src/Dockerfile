FROM python:3.6-alpine

RUN apk --no-cache add build-base

WORKDIR /app
ADD requirements.txt /app
RUN pip install --trusted-host pypi.python.org -r requirements.txt
RUN pip install --trusted-host pypi.python.org uvloop

ADD . /app

