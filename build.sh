#!/bin/bash
set -ev

dotnet restore ./serilog-sinks-syslog.sln
dotnet build ./src/Serilog.Sinks.Syslog/Serilog.Sinks.Syslog.csproj --framework netstandard3.1 --configuration Release

dotnet test ./test/Serilog.Sinks.Syslog.Tests/Serilog.Sinks.Syslog.Tests.csproj --framework netcoreapp3.1

cd ${TRAVIS_BUILD_DIR}
