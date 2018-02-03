@echo off
set OPENSSL_CONF=openssl.cnf

echo -- Generating CA Certificate --
..\tools\openssl\openssl.exe req -new -nodes -x509 -sha256 -subj "/O=Acme Stuff/OU=Test/CN=Test CA" -keyout ..\certs\ca.key.pem -out ..\certs\ca.pem -days 7300 -newkey rsa:2048 -extensions ca_extensions

echo -- Generating Server Certificate --
..\tools\openssl\openssl.exe req -new -nodes -sha256 -subj "/O=Acme Stuff/OU=Test/CN=localhost" -keyout ..\certs\server.key.pem -out ..\certs\server.csr -days 7300 -newkey rsa:2048
..\tools\openssl\openssl.exe x509 -req -sha256 -in ..\certs\server.csr -CA ..\certs\ca.pem -CAkey ..\certs\ca.key.pem -CAcreateserial -out ..\certs\server.pem -days 7300 -extensions server_extensions -extfile openssl.cnf
..\tools\openssl\openssl.exe pkcs12 -export -out ..\certs\server.p12 -inkey ..\certs\server.key.pem -in ..\certs\server.pem -certfile ..\certs\ca.pem -passout pass:

echo -- Generating Client Certificate --
..\tools\openssl\openssl.exe req -new -nodes -sha256 -subj "/O=Acme Stuff/OU=Test/CN=Test Client" -keyout ..\certs\client.key.pem -out ..\certs\client.csr -days 7300 -newkey rsa:2048
..\tools\openssl\openssl.exe x509 -req -sha256 -in ..\certs\client.csr -CA ..\certs\ca.pem -CAkey ..\certs\ca.key.pem -CAcreateserial -out ..\certs\client.pem -days 7300 -extensions client_extensions -extfile openssl.cnf
..\tools\openssl\openssl.exe pkcs12 -export -out ..\certs\client.p12 -inkey ..\certs\client.key.pem -in ..\certs\client.pem -certfile ..\certs\ca.pem -passout pass:

echo -- Cleaning Up --
del ..\certs\server.csr
del ..\certs\client.csr
