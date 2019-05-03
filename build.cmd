dotnet restore .\serilog-sinks-syslog.sln
dotnet build .\src\Serilog.Sinks.Syslog\Serilog.Sinks.Syslog.csproj --configuration Release

dotnet test .\test\Serilog.Sinks.Syslog.Tests\Serilog.Sinks.Syslog.Tests.csproj

dotnet pack .\src\Serilog.Sinks.Syslog -c Release
