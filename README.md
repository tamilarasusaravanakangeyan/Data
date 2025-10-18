# Database Integration Patterns Repository

This repository contains documentation, patterns, and best practices for enterprise data integration, focusing on real-world scenarios such as data pipelines, database features, and performance tuning. It is intended for architects, developers, and DBAs working with heterogeneous data systems.


## Table of Contents


### Data Pipeline
- [Salesforce to Oracle with Apache Camel](Data_Pipeline/apache_camel_route_dsl.md)
- [Salesforce to Oracle with Apache NiFi](Data_Pipeline/apache_nifi.md)
- [Salesforce to Oracle with Striim](Data_Pipeline/striim_salesforce_reader_cdc.md)
- [Salesforce to Oracle via IBM MQ](Data_Pipeline/IBM.md)

---

### Database
- [Encrypting Data at Rest in Oracle Exadata](Database/Encryption_Data_at_Rest.md)
- [Expose Data from Oracle to SQL](Database/Expose_data_oracle_to_sql.md)
- [Expose Table as DB Link](Database/Expose_table_as_db_link.md)
- [IP Whitelisting](Database/IP_Whitelisting.MD)
- [Long Running Query](Database/Long_Running_Query.MD)
- [Oracle Database Unique Features](Database/Oracle_Database_Unique_Features.md)
- [Performance Measurement with AWR](Database/Performance_measure_awr.md)
- [Performance Tuning with Partitioning](Database/Performance_tune_partition.md)
- [Table Database Size Daily Email](Database/Table_Database_Size_Daily_email.MD)

## Blog Summary: Data Pipeline from Salesforce to Oracle

The blog post [Data Pipeline: Salesforce to Oracle via IBM MQ](Data_Pipeline/IBM.md) describes an enterprise integration pattern for extracting data from Salesforce using Azure Logic Apps, transforming and queuing it in IBM MQ, and loading it into an Oracle database with a .NET application. The article covers the architecture, implementation approach, best practices (idempotency, error handling, monitoring, security, scalability), and strategies for data loss prevention and resiliency. This pattern enables reliable, scalable, and secure data movement between cloud and on-premises systems.

