#!/bin/sh
set -eu

: "${RUNESCAPE_CI_READ_KEY:?repository-only read key is required}"
test "$(git rev-parse --is-shallow-repository)" = false
umask 077
key_dir=$(mktemp -d)
trap 'rm -rf "$key_dir"' EXIT
trap 'exit 1' HUP INT TERM
printf '%s\n' "$RUNESCAPE_CI_READ_KEY" > "$key_dir/key"
unset RUNESCAPE_CI_READ_KEY
printf '%s\n' 'github.com ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl' > "$key_dir/known_hosts"
export GIT_SSH_COMMAND="ssh -F /dev/null -i $key_dir/key -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes -o HostKeyAlias=github.com -o UserKnownHostsFile=$key_dir/known_hosts -o GlobalKnownHostsFile=/dev/null"
timeout -k 15 180 git fetch --no-recurse-submodules --no-tags \
  ssh://git@ssh.github.com:443/scowalt/runescape-progression.git \
  '+refs/heads/*:refs/remotes/origin/*' '+refs/tags/*:refs/tags/*'
unset GIT_SSH_COMMAND
git rev-parse HEAD
git for-each-ref --format='%(objectname) %(refname)' refs/remotes/origin refs/tags
