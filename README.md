# JetCS
## A Client Server database system utilising the Jet database engine
The Jet database system utilised in Microsoft Access has extensive functionality but has known problems with corruption when used over poor network connections.  When utilised on a single computer it is very solid and reliable.  This leads the the conclusion that if it can be run on a single computer with a single server application dispatching and receiving the data, then the Jet database system can form the backend of a viable client server database.

## Why do this?
There are large numbers of Jet databases that could be served via a client server arrangement rather than over a file share.  The file can be made available by simply dropping it into the data path of JetCS and configuring some login credentials.

## What next
A real benefit would be have have an ODBC driver that can communicate with the server.  This would make it possible to ODBC link the tables of existing MS Access databases and allow those databases to continue to run without much apparent change to the users

## View the Wiki
The Wiki will contain documentation on how it works and how to use it
https://github.com/ravetroll/JetCS/wiki
