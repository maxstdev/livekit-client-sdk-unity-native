#!/usr/bin/env zsh
cd $(dirname "$0")
./protoc-3.10.1-osx-x86_64/bin/protoc -I=./ --csharp_out=./ ./*.proto