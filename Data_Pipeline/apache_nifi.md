# Building an Enterprise Data Pipeline with Apache NiFi: Extracting from Salesforce, Transforming, and Loading into Oracle

In today's data-driven enterprises, seamless integration between cloud applications and on-premises databases is critical. The **Enterprise Integration Pattern (EIP)** for data pipelines typically involves extracting data from a source (like Salesforce), transforming it as needed, and loading it into a target system (such as Oracle Database). **Apache NiFi** stands out as a powerful, real-time data ingestion and transformation tool for implementing such pipelines.

## Why Apache NiFi for ETL?

- **Real-time Data Ingestion:** NiFi supports low-latency, event-driven data flows.
- **Visual Flow Design:** Drag-and-drop interface for building complex pipelines.
- **Extensive Connectivity:** Built-in processors for Salesforce, Oracle, and many other systems.
- **Scalability & Reliability:** Supports clustering, back pressure, and guaranteed delivery.

## Implementation: ETL Pipeline from Salesforce to Oracle

### 1. Extract: Reading Data from Salesforce

- Use the **Salesforce Data Reader** processor.
- Configure it to connect to your Salesforce instance (OAuth credentials, SOQL query, etc.).
- Set the scheduling to run every 2 minutes for near real-time extraction.

### 2. Transform: Mapping Data

- Use NiFi's **inbuilt Record Mapper** or **UpdateRecord** processor.
- Map fields from Salesforce schema to Oracle schema as required.
- Apply any necessary data cleansing or enrichment.

### 3. Load: Writing to Oracle Database

- Use the **PutDatabaseRecord** or **PutSQL** processor.
- Configure connection to Oracle (JDBC URL, credentials).
- Ensure data types and mappings align with Oracle table structure.

### Example Flow Overview

1. **Salesforce Data Reader** (scheduled every 2 minutes)
2. → **Record Mapper** (map/transform fields)
3. → **PutDatabaseRecord** (load into Oracle)

## NiFi File Types and Configuration Artifacts


Modern NiFi (2.x+) uses several file types for configuration, versioning, and pipeline management:

- **Flow Definition (`flow.json.gz`):**
	- Stores the entire flow configuration for the NiFi instance.
- **Templates (`.json`):**
	- Exported pipeline templates (previously XML, now JSON in 2.x+).
	- Can be imported/exported and versioned in NiFi Registry.
- **Parameter Contexts (`.json`):**
	- Store environment-specific parameters (e.g., credentials, endpoints).
- **Provenance Data:**
	- Internal files tracking data lineage and flow events.
- **NiFi Registry Files:**
	- Version control for flows and templates, enabling CI/CD for data pipelines.

### Example: Template Export for Salesforce-to-Oracle Pipeline

- Exported as a JSON file (e.g., `SalesforceToOracleTemplate.json`).
- Can be stored in NiFi Registry for versioning and team collaboration.
## Sample NiFi Template JSON

Below is a simplified example of a NiFi template (exported as JSON in NiFi 2.x+) for a pipeline that extracts data from Salesforce, transforms it, and loads it into Oracle. This is for illustration; actual templates will be more detailed and environment-specific.

```json
{
	"template": {
		"name": "SalesforceToOracleETL",
		"description": "Extract from Salesforce, transform, and load into Oracle.",
		"processors": [
			{
				"type": "org.apache.nifi.processors.salesforce.GetSalesforceObject",
				"name": "Salesforce Data Reader",
				"schedulingPeriod": "2 min",
				"properties": {
					"Salesforce Object": "Account",
					"SOQL Query": "SELECT Id, Name, Industry FROM Account"
				}
			},
			{
				"type": "org.apache.nifi.processors.standard.UpdateRecord",
				"name": "Record Mapper",
				"properties": {
					"Record Reader": "JsonTreeReader",
					"Record Writer": "JsonRecordSetWriter",
					"Mapping": "{ 'Name': '/AccountName', 'Industry': '/AccountIndustry' }"
				}
			},
			{
				"type": "org.apache.nifi.processors.standard.PutDatabaseRecord",
				"name": "Put to Oracle",
				"properties": {
					"JDBC Connection Pool": "OracleDBCPService",
					"Table Name": "ORACLE_ACCOUNTS"
				}
			}
		],
		"connections": [
			{
				"source": "Salesforce Data Reader",
				"destination": "Record Mapper"
			},
			{
				"source": "Record Mapper",
				"destination": "Put to Oracle"
			}
		]
	}
}
```

> **Note:** In a real NiFi environment, templates are more complex and include controller services, parameter contexts, and full processor configurations. This sample is for conceptual understanding.

## Summary

Apache NiFi provides a robust, scalable, and user-friendly platform for building enterprise ETL pipelines. By leveraging NiFi's processors and modern configuration management, organizations can efficiently extract data from Salesforce, transform it, and load it into Oracle databases in near real-time, all while maintaining version control and operational transparency.

---
