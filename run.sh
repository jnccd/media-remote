#!/usr/bin/env bash

# Change to the directory where the script resides
cd -- "$(dirname -- "${BASH_SOURCE[0]}")" || exit 1

cd Server
dotnet run -c Release -- "$@"