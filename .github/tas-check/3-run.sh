#!/bin/bash
# Runs the requested TAS and checks the result.
# Parameter: path of the TAS to run

set -xeo pipefail

docker run \
	--volume "/tmp/everest-pr-tas-check:/home/ubuntu/tas" \
	--rm \
	--name celeste celeste \
	--sync-check-file "/home/ubuntu/$1" \
	--sync-check-result /home/ubuntu/tas/result.json

[ "`jq -r '.entries[].status' /tmp/everest-pr-tas-check/result.json`" == "success" ]