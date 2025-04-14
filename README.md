| :mega: Important notice if you're upgrading between 2.x and 3.x |
|--------------|
| If you're upgrading from 2.x to 3.x, and use the `SyslogTcpConfig` or `SyslogLoggerConfigurationExtensions.TcpSyslog()` extension method, there is a breaking change to the interface. The `SyslogTcpConfig.SecureProtocols` property has been replaced with just a boolean, `SyslogTcpConfig.UseTls`. Similarly, the `secureProtocols` parameter of the extension method has also been replaced with just a `useTls` boolean. If you were using either of those and passing in a value such as `SslProtocols.Tls12`, then you will simply pass in `true` for the new boolean value. If you were using `SslProtocols.None`, then you will pass in `false` for the new boolean value. |
||
| If you're using .NET Framework 4.6.2, please read the following [Transport Layer Security (TLS) best practices with the .NET Framework](https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls), specifically, the section [For .NET Framework 4.6 - 4.6.2 and not WCF](https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls#for-net-framework-46---462-and-not-wcf). That leads to [Switch.System.Net.DontEnableSystemDefaultTlsVersions](https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls#switchsystemnetdontenablesystemdefaulttlsversions), in which it is recommended to set that switch value to `false`. This can be done in code with the following: ```AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);``` |


# Serilog.Sinks.SyslogMessages

[![Windows Build Status](https://ci.appveyor.com/api/projects/status/github/IonxSolutions/serilog-sinks-syslog?svg=true&branch=master)](https://ci.appveyor.com/project/ionx-solutions/serilog-sinks-syslog)
[![Linux Build status](https://travis-ci.org/IonxSolutions/serilog-sinks-syslog.svg?branch=master)](https://travis-ci.org/IonxSolutions/serilog-sinks-syslog)
[![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.SyslogMessages.svg)](https://www.nuget.org/packages/Serilog.Sinks.SyslogMessages)

A [Serilog](https://serilog.net) sink that logs events to remote syslog servers using both UDP and TCP (including over TLS), and can also use POSIX libc syslog functions to write to the local syslog service on Linux systems. Both RFC3164 and RFC5424 format messages are supported.

### Getting started

Install the [Serilog.Sinks.SyslogMessages](https://www.nuget.org/packages/Serilog.Sinks.SyslogMessages) package from NuGet:

```powershell
Install-Package Serilog.Sinks.SyslogMessages
```

To configure the sink to write messages to a remote syslog service over UDP, call `WriteTo.UdpSyslog()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.UdpSyslog("10.10.10.14")
    .CreateLogger();
```

To configure the sink to write messages to a remote syslog service over TCP, call `WriteTo.TcpSyslog()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.TcpSyslog("10.10.10.14")
    .CreateLogger();
```

To configure the sink to write messages to the local syslog service on Linux systems, call `WriteTo.LocalSyslog()` during logger configuration:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.LocalSyslog()
    .CreateLogger();
```

A number of optional parameters are available for more advanced configurations, with more details in the following sections.

### Message Format
This sink supports RFC3164 and RFC5424 format messages, as well as a basic 'local' format which is suitable for use with the `LocalSyslog` sink. The default is RFC3164 for the UDP sink, and RFC5424 for the TCP sink. RFC5424 is more capable format, and should be used when possible - for example, it supports full timestamps that include the local time offset. It also supports structured data, and these sinks will write Serilog properties to the STRUCTURED-DATA field.

To configure the format, use the `format` parameter:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.UdpSyslog("10.10.10.14", format: SyslogFormat.RFC5424)
    .CreateLogger();
```

#### Examples
An example of an RFC3164 message:

```
<12>Dec 19 04:01:02 MYHOST MyApp[1912]: [Source.Context] This is a test message
```

An example of an RFC5424 message:

```
<12>1 2013-12-19T04:01:02.357852+00:00 MYHOST MyApp 1912 Source.Context [meta Property1="A Value" AnotherProperty="Another Value" SourceContext="Source.Context"] This is a test message
```

### Message Framing
When using TCP, messages can be framed in a variety of ways. Historically, servers have accepted messages terminated with a newline, carriage return, newline *and* carriage return, or `nul`. More fully-featured syslog servers also support a more transparent framing method, where each message is prefixed with its length. This 'octet-counting' method is described in [RFC5425](https://tools.ietf.org/html/rfc5425) and [RFC6587](https://tools.ietf.org/html/rfc6587).

To configure the framing method, use the `framingType` parameter:

```csharp
var log = new LoggerConfiguration()
    .WriteTo.TcpSyslog("10.10.10.14", framingType: FramingType.OCTET_COUNTING)
    .CreateLogger();
```

### Secure Communication
The TcpSink supports TLS-enabled syslog servers that implement [RFC5425](https://tools.ietf.org/html/rfc5425) (such as Rsyslog). Mutual authentication is also supported. A full example:

```csharp
var tcpConfig = new SyslogTcpConfig
{
    Host = "10.10.10.14",
    Port = 6514,
    Formatter = new Rfc5424Formatter(),
    Framer = new MessageFramer(FramingType.OCTET_COUNTING),
    UseTls = true,
    CertProvider = new CertificateFileProvider("MyClientCert.pfx"),
    CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
    {
        // Check the server certificate here
        return true;
    }
};

var log = new LoggerConfiguration()
    .WriteTo.TcpSyslog(tcpConfig)
    .CreateLogger();
```

`SyslogTcpConfig` properties:

**UseTls**: If `true`, the connection to the Syslog server will be secured using SSL/TLS, as chosen by the operating system, while negotiating with the Syslog server. Note that the server must be configured to support TLS in order for the connection to succeed. If set to `false`, the sink will connect to the Syslog server over an unsecure TCP connection.

**CertProvider**: can optionally be set if the syslog server requires client authentication. Various `ICertificateProvider`s are provided, to load a certificate from disk, the Certificate Store, or for you to pass in a certificate from any other source.

**CertificateSelectionCallback**: this can be used instead of the custom `CertProvider` which utilizes the .NET [LocalCertificateSelectionCallback]( https://learn.microsoft.com/en-us/dotnet/api/system.net.security.localcertificateselectioncallback?view=netframework-4.6.2) functionality used by things like the [SslStream](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=netframework-4.6.2), which is exactly what is used within this Serilog.Sinks.SyslogMessages component. You can look at the Unit Tests for an example.

**CertValidationCallback**: can optionally be set if you want to perform your own validation of the syslog server's certificate. If not set, the system default will be used (the certificate must chain to a trusted root in the Certificate Store).

### Additional optional parameters

```csharp
var log = new LoggerConfiguration()
    .WriteTo.UdpSyslog(host: "10.10.10.14", port: 514, appName: "my-app", format: SyslogFormat.RFC5424, facility: SyslogFacility.Local1, outputTemplate: "{Message}")
    .CreateLogger();
```

**host** and **port** define the address the remote syslog server is listening at.

**app-name** is the name of your application, which will be included in the `TAG` field when using RFC3164 format, or the `APP-NAME` field when using RFC5424 format. If not set, this will be defaulted to the name of the current process.

**format** defines whether messages are sent in RFC3164, RFC5424 or 'local' format - see the Message Format section above for more information.

**facility** The syslog 'facility' defines the category of the system or application that is generating the log message. The default is `Local0`, but it can be set to any of the values as defined in the syslog [RFCs](https://tools.ietf.org/html/rfc5424#section-6.2.1):

```
Kernel
User
Mail
Daemons
Auth
Syslog
LPR
News
UUCP
Cron
Auth2
FTP
NTP
LogAudit
LogAlert
Cron2
Local0
Local1
Local2
Local3
Local4
Local5
Local6
Local7
```

**outputTemplate** Controls the format of the 'body' part of messages. See https://github.com/serilog/serilog/wiki/Formatting-Output.

**restrictedToMinimumLevel** The minimum level for log events to pass through the sink. See https://github.com/serilog/serilog/wiki/Configuration-Basics#minimum-level.

## Rsyslog configuration
On most systems, Rsyslog defaults to only accepting messages locally through the POSIX libc syslog functions. If you want to enable support for RFC5424 format messages, or you want to accept messages from remote hosts, you will need to enable support for either/both UDP and TCP. More information can be found [here](http://www.rsyslog.com/receiving-messages-from-a-remote-system/).

### Enabling support for TLS
Install the `rsyslog-gnutls` package (e.g. on CentOS; adjust the command to suit your package manager):

```bash
# yum install rsyslog-gnutls
```

For **testing**, you can generate a self-signed certificate:

```bash
# openssl genrsa -out /etc/pki/tls/private/rsyslog-key.pem 2048
# openssl req -x509 -new -key /etc/pki/tls/private/rsyslog-key.pem -out /etc/pki/tls/certs/rsyslog.pem -days 3650
```

Update `/etc/rsyslog.conf` to include:

```
# Set certificate locations
$DefaultNetstreamDriver gtls
$DefaultNetstreamDriverCAFile /etc/pki/tls/certs/rsyslog.pem
$DefaultNetstreamDriverCertFile /etc/pki/tls/certs/rsyslog.pem
$DefaultNetstreamDriverKeyFile /etc/pki/tls/private/rsyslog-key.pem

# Listen for TCP connections on port 6514
$ModLoad imtcp
$InputTCPServerRun 6514

# Enable TLS
$InputTCPServerStreamDriverMode 1

# Don't authenticate clients.
$InputTCPServerStreamDriverAuthMode anon

# If you *do* want clients to authenticate with a client certificate, then either:
#
# 1) Set $InputTCPServerStreamDriverAuthMod to x509/certvalid, which will validate the the client
#    has presented a certificate signed by the same CA as the one that signed rsyslog's certificate
#
#   OR to add *additional* checks, use either of:
#
# 2) Set $InputTCPServerStreamDriverAuthMod to x509/fingerprint and configure valid client cert SHA1
#    thumbprints by including a $InputTCPServerStreamDriverPermittedPeer for each client cert - this will
#    additionally check the fingerprint of client certs matches one of the provided values. Example:
#    $InputTCPServerStreamDriverPermittedPeer SHA1:6C:8E:A2:C4:39:BF:56:0E:72:A0:21:F2:D2:82:64:CA:4A:D0:48:8B
#
# 3) Set $InputTCPServerStreamDriverAuthMod to x509/name and configure valid client cert CN (common
#    name) values by including a $InputTCPServerStreamDriverPermittedPeer for each name - this will
#    additionally check the CN of client certs matches one of the provided values (wilcards can be used).
#    Example:
#    $InputTCPServerStreamDriverPermittedPeer *.example.com
#    $InputTCPServerStreamDriverPermittedPeer host.domain.com
```

### Enabling support for RFC5424 messages
Rsyslog normally defaults to RFC3164 format messages, but can write RFC5424 format messages by changing the `$ActionFileDefaultTemplate` property in `/etc/rsyslog.conf`:

```
$ActionFileDefaultTemplate RSYSLOG_SyslogProtocol23Format
```

_Copyright &copy; 2025 [Ionx Solutions](https://www.ionxsolutions.com) - Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html)._
