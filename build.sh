#!/bin/bash
set -ev

dotnet restore ./serilog-sinks-syslog.sln
dotnet build ./test/Serilog.Sinks.Syslog.Tests/Serilog.Sinks.Syslog.Tests.csproj --configuration Release

# dotnet-xunit is a CLI tool that can only be executed from in the test folder
cd ./test/Serilog.Sinks.Syslog.Tests

dotnet xunit -framework netcoreapp1.1 -fxversion 1.1.7
dotnet xunit -framework netcoreapp2.0 -fxversion 2.0.6

cd ${TRAVIS_BUILD_DIR}
