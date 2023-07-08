dotnet restore .\serilog-sinks-syslog.sln
dotnet build .\src\Serilog.Sinks.Syslog\Serilog.Sinks.Syslog.csproj --configuration Release -p:EnableSourceLink=true

dotnet test .\test\Serilog.Sinks.Syslog.Tests\Serilog.Sinks.Syslog.Tests.csproj --logger "console;verbosity=normal"

dotnet pack .\src\Serilog.Sinks.Syslog -c Release
