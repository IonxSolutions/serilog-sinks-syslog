set START_DIR=%cd%

dotnet restore .\serilog-sinks-syslog.sln
dotnet build .\src\Serilog.Sinks.Syslog\Serilog.Sinks.Syslog.csproj --configuration Release

rem dotnet-xunit is a CLI tool that can only be executed from in the test folder
cd .\test\Serilog.Sinks.Syslog.Tests
dotnet xunit

cd %START_DIR%

dotnet pack .\src\Serilog.Sinks.Syslog -c Release
