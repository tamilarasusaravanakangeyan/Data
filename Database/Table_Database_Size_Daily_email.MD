## Automating Daily Table-Wise Database Size Email in Oracle Exaccdata

Monitoring table sizes and notifying application teams helps manage storage proactively. In Oracle Exadata, you can automate daily emails with table-wise size details using built-in features and ACLs.

### 1. Query Table Size in Oracle
Use the following query to get the size of each table in a schema:

```sql
SELECT table_name,
			 ROUND(SUM(bytes)/1024/1024,2) AS size_mb
FROM dba_segments
WHERE owner = 'APP_SCHEMA'
	AND segment_type = 'TABLE'
GROUP BY table_name
ORDER BY size_mb DESC;
```

### 2. Prepare the Email Body
Format the query result as a readable table in the email body. Example (PL/SQL):

```plsql
DECLARE
	l_body VARCHAR2(4000);
BEGIN
	l_body := 'Table Name | Size (MB)' || CHR(10);
	FOR rec IN (
		SELECT table_name, ROUND(SUM(bytes)/1024/1024,2) AS size_mb
		FROM dba_segments
		WHERE owner = 'APP_SCHEMA' AND segment_type = 'TABLE'
		GROUP BY table_name
		ORDER BY size_mb DESC
	) LOOP
		l_body := l_body || rec.table_name || ' | ' || rec.size_mb || CHR(10);
	END LOOP;
	-- Send email (see below)
END;
```

### 3. Send Email Using ACL and UTL_MAIL
First, configure ACL to allow email sending:

```sql
BEGIN
	DBMS_NETWORK_ACL_ADMIN.CREATE_ACL(
		acl         => 'email_acl.xml',
		description => 'Allow email',
		principal   => 'APP_USER',
		is_grant    => TRUE,
		privilege   => 'connect'
	);
	DBMS_NETWORK_ACL_ADMIN.ASSIGN_ACL(
		acl  => 'email_acl.xml',
		host => 'smtp.yourdomain.com'
	);
END;
```

Then, use UTL_MAIL to send the email:

```plsql
BEGIN
	UTL_MAIL.send(
		sender     => 'dbadmin@yourdomain.com',
		recipients => 'appteam@yourdomain.com',
		subject    => 'Daily Table Size Report',
		message    => l_body
	);
END;
```

### 4. Automate with a Scheduled Job
Use DBMS_SCHEDULER to run the script daily:

```sql
BEGIN
	DBMS_SCHEDULER.create_job(
		job_name        => 'SEND_TABLE_SIZE_EMAIL',
		job_type        => 'PLSQL_BLOCK',
		job_action      => 'BEGIN -- your email PL/SQL block here END;',
		start_date      => SYSDATE,
		repeat_interval => 'FREQ=DAILY;BYHOUR=8',
		enabled         => TRUE
	);
END;
```

---

**Summary:**
By combining table size queries, email formatting, ACL configuration, and scheduled jobs, you can automate daily database size notifications to application teams in Oracle Exadata.
