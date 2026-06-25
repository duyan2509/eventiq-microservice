#!/bin/sh
# Wrapper: run k6 and strip non-ASCII (progress bar, ✓/✗) so that
# `az container logs` on a Windows cp1252 console doesn't crash.
k6 run "$@" 2>&1 | tr -cd '\11\12\15\40-\176'
