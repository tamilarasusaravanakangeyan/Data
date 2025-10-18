# Enterprise Integration Pattern: Data Pipeline from Salesforce to Oracle with Striim

Modern enterprises often need to integrate cloud applications like Salesforce with on-premises or cloud databases such as Oracle. This blog explores two approaches for building a robust data pipeline using Striim: batch ETL and real-time CDC (Change Data Capture).

## 1. Batch ETL Pipeline: Extract, Transform, Load

**Implementation:**

- Use Striim’s Salesforce Data Reader to extract data from Salesforce.
- Transform data using Striim’s inbuilt Mapper.
- Load data into Oracle Database.
- Schedule pipeline to run every 2 minutes.


### Enhanced Sample TQL to Read Data from Salesforce

```sql

CREATE SOURCE SalesforceReader USING SalesforceReader (
	Username = 'your_salesforce_username',
	Password = 'your_salesforce_password',
	SecurityToken = 'your_salesforce_token',
	SOQL = 'SELECT Id, Name, Industry FROM Account',
	QueryInterval = 120, -- Polling interval in seconds (2 minutes)
	StartTimestamp = '2025-10-01T00:00:00Z', -- Start reading from this timestamp
	ApiEndpoint = 'https://yourinstance.salesforce.com', -- Salesforce API endpoint
	ThreadPoolSize = 4 -- Number of threads for parallel processing
) OUTPUT TO SalesforceReaderStream;


CREATE CQ MapToOracleCQ
INSERT INTO OracleTargetStream
SELECT Id, Name, Industry FROM SalesforceReaderStream
OUTPUT TO OracleTarget;

CREATE TARGET OracleTarget USING OracleWriter (
	Username = 'oracle_user',
	Password = 'oracle_password',
	ConnectionURL = 'jdbc:oracle:thin:@//host:port/service',
	TableName = 'ACCOUNT'
);
```

**Notes:**
- The `QueryInterval` parameter sets the polling interval to 2 minutes (120 seconds).
- The Mapper (CQ) transforms the data from the Salesforce stream to match the Oracle schema.

## 2. Real-Time CDC Pipeline: Change Data Capture

**Implementation:**

- Use Striim’s Salesforce CDC plugin to capture changes (inserts, updates, deletes) in Salesforce objects (e.g., Account).
- Transform data using Striim’s Mapper.
- Load changes into Oracle Exadata DB in real time.

### CDC Flow

1. **Salesforce Create/Update/Delete Event:** When an Account is created, updated, or deleted in Salesforce, a CDC event is generated.
2. **Striim CDC Reader:** The CDC plugin reads the event and streams it into Striim.
3. **Transformation:** Mapper transforms the CDC event to match Oracle schema.
4. **Load:** Data is written to Oracle Exadata DB instantly.


### Enhanced Sample TQL for CDC Pipeline

```sql

CREATE SOURCE SalesforceCDCReader USING SalesforceCDCReader (
	Username = 'your_salesforce_username',
	Password = 'your_salesforce_password',
	SecurityToken = 'your_salesforce_token',
	ObjectName = 'Account',
	EventType = 'CREATE,UPDATE,DELETE',
	StartTimestamp = '2025-10-01T00:00:00Z', -- Start CDC from this timestamp
	ThreadPoolSize = 4 -- Number of threads for parallel CDC event processing
) OUTPUT TO SalesforceCDCStream;


CREATE CQ MapCDCToOracleCQ
INSERT INTO OracleCDCStream
SELECT Id, Name, Industry, EventType FROM SalesforceCDCStream
OUTPUT TO OracleCDCWriter;

CREATE TARGET OracleCDCWriter USING OracleWriter (
	Username = 'oracle_user',
	Password = 'oracle_password',
	ConnectionURL = 'jdbc:oracle:thin:@//host:port/service',
	TableName = 'ACCOUNT_CDC'
);
```

**Notes:**
- The CDC pipeline reacts instantly to changes in Salesforce, ensuring Oracle is always up to date.
- The Mapper can handle event types and apply logic for inserts, updates, and deletes.

## Conclusion

Striim enables both scheduled batch ETL and real-time CDC integration patterns for Salesforce-to-Oracle pipelines. Choose batch ETL for periodic sync, or CDC for low-latency, event-driven integration. Both approaches leverage Striim’s powerful connectors, mappers, and orchestration features for enterprise-grade data movement.
