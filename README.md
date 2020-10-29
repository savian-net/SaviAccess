# SaviAccess
SaviAccess allows any ODBC connection to be used to bring data into a SAS dataset. 

## Overview

SaviAccess allows any ODBC connection to be used to bring data into a SAS dataset. It has 2 functionalities, 1 is optional:

	- [OPTIONAL] Generate the necessary SAS code by looking at the ODBC metadata for the query
	- Read in the ODBC source and bring it into SAS


IF YOU WANT SAVIACCESS TO GENERATE THE SAS CODE:

	1. Execute the SaviAccess.exe and set the s flag (plus others that are needed) to a true value. This will generate the SAS code needed for the particular read. For example:

```SaviAccess.exe -q "SELECT * FROM [WideWorldImporters].[Sales].[OrderLines]" -t "OrderLines" -o "driver=ODBC Driver 17 for SQL Server;Server=INTERNAL-SERVER;Database=WideWorldImporters;Trusted_Connection=Yes;" -s true -w "c:\temp\SaviAccess\work"

	2. Open the generated SAS program found in the work area. Make any necessary changes to the data access section


# Sample of what is generated (or what needs to be coded by hand) 

# EXECUTE THE SAS CODE

  FILENAME DATAPIPE PIPE "c:\temp\SaviAccess.exe -s false -q ""SELECT * FROM [WideWorldImporters].[Sales].[OrderLines]"" -t ""OrderLines"" -o ""driver=ODBC Driver 17 for SQL Server;Server=INTERNAL-SERVER;Database=WideWorldImporters;Trusted_Connection=Yes;"" ";

  DATA OrderLines / VIEW=OrderLines;
    INFILE DATAPIPE DLM='09'x DSD MISSOVER;
    INPUT
	 ORDERLINEID 
	 ORDERID 
	 STOCKITEMID 
	 DESCRIPTION $
	 PACKAGETYPEID 
	 QUANTITY 
	 UNITPRICE 
	 TAXRATE 
	 PICKEDQUANTITY 
	 PICKINGCOMPLETEDWHEN anydtdte12.
	 LASTEDITEDBY 
	 LASTEDITEDWHEN anydtdte12.
     ;
  RUN;

## Determine connection string 

[Connection Strings](https://www.connectionstrings.com/)
[How to Create a Connection String Using UDL File](https://social.technet.microsoft.com/wiki/contents/articles/1409.how-to-create-a-sql-connection-string-for-an-application-udl-file.aspx)

