#!/bin/bash

# This file is run directly on the build host

case "$1" in
  "--integration")
	shift
	apk add py-pip
	pip install docker-compose==1.22.0
	IMAGE_TAG=${1} docker-compose -f docker-compose.${2}.yml run test
	docker cp $(docker ps -f label=test -a -l -q):/sln/results.xml .
	;;
	*)
  echo Invalid command, use '--build', '--test', '--integration' or '--publish'
    exit 1
    ;;
esac