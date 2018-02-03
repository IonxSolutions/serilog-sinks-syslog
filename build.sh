#!/bin/bash
set -ev

dotnet restore ./serilog-sinks-syslog.sln --runtime netstandard2.0
dotnet build ./src/Serilog.Sinks.Syslog/Serilog.Sinks.Syslog.csproj  --runtime netstandard2.0 --configuration Release

# dotnet-xunit is a CLI tool that can only be executed from in the test folder
cd ./test/Serilog.Sinks.Syslog.Tests
dotnet xunit -framework netcoreapp2.0

cd ${TRAVIS_BUILD_DIR}

dotnet pack ./src/Serilog.Sinks.Syslog -c Release
