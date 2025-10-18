# Automated Email Notifications for Long Running Database Queries: A Support Team's Best Friend

*Published on October 18, 2025*

Database performance monitoring is crucial for maintaining system health, but manually tracking long-running queries can be a nightmare for support teams. What if your database could automatically notify your support team when queries exceed acceptable thresholds? In this blog post, we'll explore how to implement automated email notifications for long-running queries across different database platforms.

## Table of Contents

- [Why Monitor Long Running Queries?](#why-monitor-long-running-queries)
- [Oracle Database Implementation](#oracle-database-implementation)
- [SQL Server Implementation](#sql-server-implementation)
- [PostgreSQL Implementation](#postgresql-implementation)
- [MySQL Implementation](#mysql-implementation)
- [Best Practices](#best-practices)
- [Troubleshooting Common Issues](#troubleshooting-common-issues)
- [Conclusion](#conclusion)

## Why Monitor Long Running Queries?

Long-running queries can:
- **Impact Performance**: Consume excessive CPU, memory, and I/O resources
- **Block Other Operations**: Hold locks preventing other transactions from completing
- **Indicate Problems**: Signal inefficient code, missing indexes, or data issues
- **Affect User Experience**: Cause application timeouts and poor response times

Automated email notifications help support teams:
- **React Quickly**: Get immediate alerts about performance issues
- **Maintain SLAs**: Ensure response times meet service level agreements
- **Prevent Escalation**: Address problems before they affect end users
- **Gather Evidence**: Collect performance data for analysis

## Oracle Database Implementation

Oracle provides robust email capabilities through the `UTL_MAIL` package and query monitoring through various system views.

### Setting Up Email Configuration

First, configure the database to send emails. **Note: Oracle requires ACL (Access Control List) configuration for network access.**

```sql
-- Step 1: Configure SMTP server (requires DBA privileges)
ALTER SYSTEM SET smtp_out_server = 'smtp.yourcompany.com:587' SCOPE=BOTH;

-- Step 2: Create ACL for network access (REQUIRED - Cannot send emails without this)
-- Replace 'your_monitoring_user' with your actual monitoring user
BEGIN
    -- Create ACL for SMTP access
    DBMS_NETWORK_ACL_ADMIN.CREATE_ACL(
        acl         => 'smtp_acl.xml',
        description => 'ACL for SMTP email access',
        principal   => 'YOUR_MONITORING_USER',  -- Replace with actual username
        is_grant    => TRUE,
        privilege   => 'connect'
    );
    
    -- Add resolve privilege for DNS lookups
    DBMS_NETWORK_ACL_ADMIN.ADD_PRIVILEGE(
        acl       => 'smtp_acl.xml',
        principal => 'YOUR_MONITORING_USER',  -- Replace with actual username
        is_grant  => TRUE,
        privilege => 'resolve'
    );
    
    -- Assign ACL to SMTP server (adjust hostname and port as needed)
    DBMS_NETWORK_ACL_ADMIN.ASSIGN_ACL(
        acl  => 'smtp_acl.xml',
        host => 'smtp.yourcompany.com',  -- Your SMTP server
        lower_port => 587,               -- SMTP port (587 for TLS, 25 for standard, 465 for SSL)
        upper_port => 587
    );
    
    -- Commit ACL changes
    COMMIT;
END;
/

-- Step 3: Grant necessary privileges to the monitoring user
GRANT EXECUTE ON UTL_MAIL TO your_monitoring_user;
GRANT EXECUTE ON UTL_SMTP TO your_monitoring_user;  -- Alternative email package

-- Step 4: Verify ACL configuration
SELECT acl, principal, privilege, is_grant 
FROM dba_network_acl_privileges 
WHERE acl = 'smtp_acl.xml';

-- Check ACL assignments
SELECT acl, host, lower_port, upper_port 
FROM dba_network_acls 
WHERE acl = 'smtp_acl.xml';
```

### Alternative: Using UTL_SMTP (More Control, Still Requires ACL)

If UTL_MAIL doesn't work or you need more control, you can use UTL_SMTP:

```sql
-- Example procedure using UTL_SMTP instead of UTL_MAIL
CREATE OR REPLACE PROCEDURE send_email_via_smtp(
    p_from    VARCHAR2,
    p_to      VARCHAR2,
    p_subject VARCHAR2,
    p_body    CLOB
) AS
    l_connection UTL_SMTP.connection;
    l_smtp_server VARCHAR2(100) := 'smtp.yourcompany.com';
    l_smtp_port   NUMBER := 587;
BEGIN
    -- Open SMTP connection
    l_connection := UTL_SMTP.open_connection(l_smtp_server, l_smtp_port);
    
    -- SMTP handshake
    UTL_SMTP.helo(l_connection, l_smtp_server);
    
    -- Set sender
    UTL_SMTP.mail(l_connection, p_from);
    
    -- Set recipient
    UTL_SMTP.rcpt(l_connection, p_to);
    
    -- Send message
    UTL_SMTP.open_data(l_connection);
    UTL_SMTP.write_data(l_connection, 'From: ' || p_from || UTL_TCP.crlf);
    UTL_SMTP.write_data(l_connection, 'To: ' || p_to || UTL_TCP.crlf);
    UTL_SMTP.write_data(l_connection, 'Subject: ' || p_subject || UTL_TCP.crlf);
    UTL_SMTP.write_data(l_connection, 'Content-Type: text/html' || UTL_TCP.crlf);
    UTL_SMTP.write_data(l_connection, UTL_TCP.crlf);
    UTL_SMTP.write_data(l_connection, p_body);
    UTL_SMTP.close_data(l_connection);
    
    -- Close connection
    UTL_SMTP.quit(l_connection);
    
EXCEPTION
    WHEN OTHERS THEN
        -- Ensure connection is closed on error
        IF UTL_SMTP.connection_open(l_connection) THEN
            UTL_SMTP.quit(l_connection);
        END IF;
        RAISE;
END;
/
```

### Troubleshooting ACL Issues

```sql
-- Check if ACL exists and is properly configured
SELECT * FROM dba_network_acls WHERE acl LIKE '%smtp%';

-- Check ACL privileges
SELECT * FROM dba_network_acl_privileges WHERE acl LIKE '%smtp%';

-- Test network connectivity (replace with your SMTP server details)
SELECT UTL_HTTP.request('http://smtp.yourcompany.com:587') FROM dual;

-- If ACL issues persist, you may need to drop and recreate:
BEGIN
    DBMS_NETWORK_ACL_ADMIN.DROP_ACL('smtp_acl.xml');
    COMMIT;
END;
/
-- Then recreate using the CREATE_ACL commands above
```

### Alternative Solutions When ACL Cannot Be Configured

**If you cannot configure ACL due to security restrictions, consider these alternatives:**

#### Option 1: File-Based Alerting with External Monitor

```sql
-- Modified procedure that writes to file instead of sending email directly
CREATE OR REPLACE PROCEDURE monitor_long_running_queries_file AS
    v_query_count NUMBER := 0;
    v_alert_content CLOB;
    v_threshold NUMBER := 300; -- 5 minutes in seconds
    
    CURSOR long_queries IS
        SELECT s.sid, s.serial#, s.username, s.program, s.machine, s.sql_id,
               ROUND((SYSDATE - s.sql_exec_start) * 24 * 60 * 60) as duration_seconds,
               SUBSTR(t.sql_text, 1, 200) as sql_text_short
        FROM v$session s
        JOIN v$sqlarea t ON s.sql_id = t.sql_id
        WHERE s.status = 'ACTIVE'
        AND s.sql_exec_start IS NOT NULL
        AND (SYSDATE - s.sql_exec_start) * 24 * 60 * 60 > v_threshold
        AND s.username IS NOT NULL
        ORDER BY duration_seconds DESC;
        
BEGIN
    -- Count queries first
    FOR rec IN long_queries LOOP
        v_query_count := v_query_count + 1;
    END LOOP;
    
    -- If long queries found, write alert file
    IF v_query_count > 0 THEN
        v_alert_content := 'ALERT_TIME=' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') || CHR(10);
        v_alert_content := v_alert_content || 'SERVER=' || SYS_CONTEXT('USERENV', 'SERVER_HOST') || CHR(10);
        v_alert_content := v_alert_content || 'DATABASE=' || SYS_CONTEXT('USERENV', 'DB_NAME') || CHR(10);
        v_alert_content := v_alert_content || 'QUERY_COUNT=' || v_query_count || CHR(10);
        v_alert_content := v_alert_content || 'THRESHOLD=' || v_threshold || CHR(10);
        
        -- Write details for each query
        FOR rec IN long_queries LOOP
            v_alert_content := v_alert_content || 'QUERY=' || rec.sid || '|' || 
                              rec.username || '|' || rec.duration_seconds || '|' || 
                              rec.sql_text_short || CHR(10);
        END LOOP;
        
        -- Write to file (requires directory object and permissions)
        DECLARE
            v_file UTL_FILE.FILE_TYPE;
        BEGIN
            v_file := UTL_FILE.FOPEN('ALERT_DIR', 'oracle_long_queries_' || 
                     TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS') || '.alert', 'W');
            UTL_FILE.PUT_LINE(v_file, v_alert_content);
            UTL_FILE.FCLOSE(v_file);
        EXCEPTION
            WHEN OTHERS THEN
                IF UTL_FILE.IS_OPEN(v_file) THEN
                    UTL_FILE.FCLOSE(v_file);
                END IF;
                RAISE;
        END;
        
        -- Log the alert
        INSERT INTO query_alert_log (
            alert_time, query_count, threshold_seconds, email_sent, error_message
        ) VALUES (
            SYSDATE, v_query_count, v_threshold, 'F', 'Alert written to file'
        );
        COMMIT;
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        INSERT INTO query_alert_log (
            alert_time, query_count, threshold_seconds, email_sent, error_message
        ) VALUES (
            SYSDATE, 0, v_threshold, 'N', SQLERRM
        );
        COMMIT;
END;
/

-- Create directory object for file output (requires DBA privileges)
CREATE OR REPLACE DIRECTORY ALERT_DIR AS '/opt/oracle/alerts/';
GRANT READ, WRITE ON DIRECTORY ALERT_DIR TO your_monitoring_user;
```

#### Option 2: Using Database Links to External Email Service

```sql
-- If you have access to another database with email capabilities
-- Create database link to external database that can send emails
CREATE DATABASE LINK email_service_db
CONNECT TO email_user IDENTIFIED BY email_password
USING 'email_service_tns';

-- Modified procedure using database link
CREATE OR REPLACE PROCEDURE monitor_queries_via_dblink AS
    v_query_count NUMBER := 0;
    v_email_body CLOB;
    
    CURSOR long_queries IS
        -- Same query as before
        SELECT s.sid, s.username, 
               ROUND((SYSDATE - s.sql_exec_start) * 24 * 60) as duration_minutes,
               SUBSTR(t.sql_text, 1, 100) as sql_text
        FROM v$session s
        JOIN v$sqlarea t ON s.sql_id = t.sql_id
        WHERE s.status = 'ACTIVE'
        AND (SYSDATE - s.sql_exec_start) * 24 * 60 > 5;
        
BEGIN
    -- Build email content
    v_email_body := '<h2>Long Running Query Alert</h2>';
    
    FOR rec IN long_queries LOOP
        v_query_count := v_query_count + 1;
        v_email_body := v_email_body || '<p>SID: ' || rec.sid || 
                       ', User: ' || rec.username || 
                       ', Duration: ' || rec.duration_minutes || ' min</p>';
    END LOOP;
    
    -- Send via database link if queries found
    IF v_query_count > 0 THEN
        EXECUTE IMMEDIATE 
            'BEGIN send_email_procedure@email_service_db(:1, :2, :3); END;'
            USING 'dba-team@yourcompany.com',
                  'Long Running Query Alert',
                  v_email_body;
    END IF;
END;
/
```

#### Option 3: External Monitoring Script (Recommended Alternative)

Create an external Python/Shell script that connects to Oracle and sends emails:

```python
#!/usr/bin/env python3
# File: /opt/scripts/oracle_long_query_monitor.py

import cx_Oracle
import smtplib
import time
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

# Configuration
DB_CONFIG = {
    'user': 'monitoring_user',
    'password': 'your_password',
    'dsn': 'localhost:1521/ORCL'
}

SMTP_CONFIG = {
    'server': 'smtp.yourcompany.com',
    'port': 587,
    'username': 'alerts@yourcompany.com',
    'password': 'smtp_password',
    'sender': 'database-alerts@yourcompany.com'
}

def check_oracle_long_queries():
    try:
        # Connect to Oracle
        connection = cx_Oracle.connect(**DB_CONFIG)
        cursor = connection.cursor()
        
        # Query for long-running queries
        query = """
        SELECT s.sid, s.serial#, s.username, s.program, s.machine,
               ROUND((SYSDATE - s.sql_exec_start) * 24 * 60 * 60) as duration_seconds,
               SUBSTR(t.sql_text, 1, 200) as sql_text
        FROM v$session s
        JOIN v$sqlarea t ON s.sql_id = t.sql_id
        WHERE s.status = 'ACTIVE'
        AND s.sql_exec_start IS NOT NULL
        AND (SYSDATE - s.sql_exec_start) * 24 * 60 * 60 > 300
        AND s.username IS NOT NULL
        ORDER BY duration_seconds DESC
        """
        
        cursor.execute(query)
        results = cursor.fetchall()
        
        if results:
            send_oracle_alert(results)
        
        cursor.close()
        connection.close()
        
    except Exception as e:
        print(f"Oracle monitoring failed: {e}")

def send_oracle_alert(query_data):
    subject = f"ALERT: {len(query_data)} Long Running Queries on Oracle"
    
    body = f"""
    <html><body>
    <h2>Oracle Long Running Query Alert - {datetime.now()}</h2>
    <p><strong>Total Queries:</strong> {len(query_data)}</p>
    
    <table border="1" cellpadding="5">
    <tr><th>SID</th><th>User</th><th>Duration (sec)</th><th>Program</th><th>SQL Text</th></tr>
    """
    
    for row in query_data:
        body += f"""
        <tr>
            <td>{row[0]}</td>
            <td>{row[2] or 'N/A'}</td>
            <td>{row[5]}</td>
            <td>{row[3] or 'N/A'}</td>
            <td>{row[6]}...</td>
        </tr>
        """
    
    body += "</table></body></html>"
    
    # Send email using external SMTP
    try:
        msg = MIMEMultipart()
        msg['From'] = SMTP_CONFIG['sender']
        msg['To'] = 'dba-team@yourcompany.com'
        msg['Subject'] = subject
        
        msg.attach(MIMEText(body, 'html'))
        
        server = smtplib.SMTP(SMTP_CONFIG['server'], SMTP_CONFIG['port'])
        server.starttls()
        server.login(SMTP_CONFIG['username'], SMTP_CONFIG['password'])
        server.send_message(msg)
        server.quit()
        
        print(f"Alert email sent: {subject}")
        
    except Exception as e:
        print(f"Failed to send email: {e}")

if __name__ == "__main__":
    check_oracle_long_queries()
```

**Schedule this external script with cron:**

```bash
# Add to crontab (runs every 5 minutes)
*/5 * * * * /usr/bin/python3 /opt/scripts/oracle_long_query_monitor.py
```

### Creating the Monitoring Procedure

```sql
CREATE OR REPLACE PROCEDURE monitor_long_running_queries AS
    v_query_count NUMBER := 0;
    v_email_body CLOB;
    v_subject VARCHAR2(200);
    v_threshold NUMBER := 300; -- 5 minutes in seconds
    
    CURSOR long_queries IS
        SELECT 
            s.sid,
            s.serial#,
            s.username,
            s.program,
            s.machine,
            s.sql_id,
            ROUND((SYSDATE - s.sql_exec_start) * 24 * 60 * 60) as duration_seconds,
            t.sql_text
        FROM v$session s
        JOIN v$sqlarea t ON s.sql_id = t.sql_id
        WHERE s.status = 'ACTIVE'
        AND s.sql_exec_start IS NOT NULL
        AND (SYSDATE - s.sql_exec_start) * 24 * 60 * 60 > v_threshold
        AND s.username IS NOT NULL
        ORDER BY duration_seconds DESC;
        
BEGIN
    -- Build email content
    v_email_body := '<html><body>';
    v_email_body := v_email_body || '<h2>Long Running Query Alert - ' || 
                   TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') || '</h2>';
    v_email_body := v_email_body || '<table border="1" cellpadding="5" cellspacing="0">';
    v_email_body := v_email_body || '<tr><th>SID</th><th>User</th><th>Duration (min)</th>' ||
                   '<th>Program</th><th>Machine</th><th>SQL ID</th><th>SQL Text</th></tr>';
    
    FOR rec IN long_queries LOOP
        v_query_count := v_query_count + 1;
        
        v_email_body := v_email_body || '<tr>';
        v_email_body := v_email_body || '<td>' || rec.sid || '</td>';
        v_email_body := v_email_body || '<td>' || NVL(rec.username, 'N/A') || '</td>';
        v_email_body := v_email_body || '<td>' || ROUND(rec.duration_seconds/60, 1) || '</td>';
        v_email_body := v_email_body || '<td>' || SUBSTR(NVL(rec.program, 'N/A'), 1, 30) || '</td>';
        v_email_body := v_email_body || '<td>' || SUBSTR(NVL(rec.machine, 'N/A'), 1, 20) || '</td>';
        v_email_body := v_email_body || '<td>' || rec.sql_id || '</td>';
        v_email_body := v_email_body || '<td>' || SUBSTR(rec.sql_text, 1, 100) || '...</td>';
        v_email_body := v_email_body || '</tr>';
    END LOOP;
    
    v_email_body := v_email_body || '</table>';
    
    -- Only send email if there are long-running queries
    IF v_query_count > 0 THEN
        v_email_body := v_email_body || '<p><strong>Total Long Running Queries: ' || 
                       v_query_count || '</strong></p>';
        v_email_body := v_email_body || '<p>Threshold: ' || v_threshold || ' seconds</p>';
        v_email_body := v_email_body || '<p>Server: ' || SYS_CONTEXT('USERENV', 'SERVER_HOST') || '</p>';
        v_email_body := v_email_body || '</body></html>';
        
        v_subject := 'ALERT: ' || v_query_count || ' Long Running Queries on ' || 
                    SYS_CONTEXT('USERENV', 'DB_NAME');
        
        -- Send email (requires ACL configuration completed above)
        UTL_MAIL.send(
            sender     => 'database-alerts@yourcompany.com',
            recipients => 'dba-team@yourcompany.com,support-team@yourcompany.com',
            subject    => v_subject,
            message    => v_email_body,
            mime_type  => 'text/html'
        );
        
        -- Log the alert
        INSERT INTO query_alert_log (
            alert_time, 
            query_count, 
            threshold_seconds,
            email_sent
        ) VALUES (
            SYSDATE, 
            v_query_count, 
            v_threshold,
            'Y'
        );
        COMMIT;
    END IF;
    
EXCEPTION
    WHEN OTHERS THEN
        -- Log error but don't fail
        INSERT INTO query_alert_log (
            alert_time, 
            query_count, 
            threshold_seconds,
            email_sent,
            error_message
        ) VALUES (
            SYSDATE, 
            0, 
            v_threshold,
            'N',
            SQLERRM
        );
        COMMIT;
END;
/
```

### Creating the Alert Log Table

```sql
CREATE TABLE query_alert_log (
    log_id NUMBER PRIMARY KEY,
    alert_time DATE NOT NULL,
    query_count NUMBER NOT NULL,
    threshold_seconds NUMBER NOT NULL,
    email_sent CHAR(1) DEFAULT 'N',
    error_message VARCHAR2(4000),
    created_date DATE DEFAULT SYSDATE
);

CREATE SEQUENCE query_alert_log_seq START WITH 1;

CREATE OR REPLACE TRIGGER query_alert_log_trg
    BEFORE INSERT ON query_alert_log
    FOR EACH ROW
BEGIN
    IF :NEW.log_id IS NULL THEN
        :NEW.log_id := query_alert_log_seq.NEXTVAL;
    END IF;
END;
/
```

### Scheduling the Monitoring Job

```sql
-- Create a job to run every 5 minutes
BEGIN
    DBMS_SCHEDULER.create_job(
        job_name        => 'LONG_QUERY_MONITOR_JOB',
        job_type        => 'PLSQL_BLOCK',
        job_action      => 'BEGIN monitor_long_running_queries; END;',
        start_date      => SYSTIMESTAMP,
        repeat_interval => 'FREQ=MINUTELY;INTERVAL=5',
        enabled         => TRUE,
        comments        => 'Monitor and alert on long running queries'
    );
END;
/

-- Check job status
SELECT job_name, state, last_start_date, next_run_date, run_count
FROM user_scheduler_jobs
WHERE job_name = 'LONG_QUERY_MONITOR_JOB';
```

## SQL Server Implementation

SQL Server offers Database Mail for email functionality and rich query monitoring capabilities.

### Setting Up Database Mail

```sql
-- Enable Database Mail (requires sysadmin privileges)
sp_configure 'show advanced options', 1;
GO
RECONFIGURE;
GO
sp_configure 'Database Mail XPs', 1;
GO
RECONFIGURE;
GO

-- Create mail profile
EXECUTE msdb.dbo.sysmail_add_profile_sp
    @profile_name = 'DatabaseAlerts',
    @description = 'Database alert notifications';

-- Add mail account
EXECUTE msdb.dbo.sysmail_add_account_sp
    @account_name = 'AlertAccount',
    @description = 'Account for database alerts',
    @email_address = 'database-alerts@yourcompany.com',
    @display_name = 'Database Alerts',
    @mailserver_name = 'smtp.yourcompany.com',
    @port = 587,
    @enable_ssl = 1,
    @username = 'alerts@yourcompany.com',
    @password = 'your_password';

-- Associate account with profile
EXECUTE msdb.dbo.sysmail_add_profileaccount_sp
    @profile_name = 'DatabaseAlerts',
    @account_name = 'AlertAccount',
    @sequence_number = 1;
```

### Creating the Monitoring Stored Procedure

```sql
CREATE PROCEDURE [dbo].[sp_MonitorLongRunningQueries]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ThresholdMinutes INT = 5;
    DECLARE @QueryCount INT = 0;
    DECLARE @EmailBody NVARCHAR(MAX) = '';
    DECLARE @Subject NVARCHAR(255);
    
    -- Create temp table for results
    CREATE TABLE #LongRunningQueries (
        session_id INT,
        login_name NVARCHAR(128),
        host_name NVARCHAR(128),
        program_name NVARCHAR(128),
        duration_minutes DECIMAL(10,2),
        cpu_time_ms BIGINT,
        reads BIGINT,
        writes BIGINT,
        sql_text NVARCHAR(MAX),
        blocking_session_id INT
    );
    
    -- Find long-running queries
    INSERT INTO #LongRunningQueries
    SELECT 
        r.session_id,
        s.login_name,
        s.host_name,
        s.program_name,
        DATEDIFF(MINUTE, r.start_time, GETDATE()) as duration_minutes,
        r.cpu_time,
        r.reads,
        r.writes,
        SUBSTRING(st.text, (r.statement_start_offset/2)+1,
            ((CASE r.statement_end_offset
                WHEN -1 THEN DATALENGTH(st.text)
                ELSE r.statement_end_offset
            END - r.statement_start_offset)/2) + 1) AS sql_text,
        r.blocking_session_id
    FROM sys.dm_exec_requests r
    INNER JOIN sys.dm_exec_sessions s ON r.session_id = s.session_id
    CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) st
    WHERE r.status IN ('running', 'runnable', 'suspended')
    AND DATEDIFF(MINUTE, r.start_time, GETDATE()) >= @ThresholdMinutes
    AND s.is_user_process = 1
    ORDER BY duration_minutes DESC;
    
    SELECT @QueryCount = COUNT(*) FROM #LongRunningQueries;
    
    -- Build email if queries found
    IF @QueryCount > 0
    BEGIN
        SET @Subject = 'ALERT: ' + CAST(@QueryCount AS VARCHAR(10)) + 
                      ' Long Running Queries on ' + @@SERVERNAME;
        
        SET @EmailBody = '<html><body>';
        SET @EmailBody = @EmailBody + '<h2>Long Running Query Alert - ' + 
                        CONVERT(VARCHAR(20), GETDATE(), 120) + '</h2>';
        SET @EmailBody = @EmailBody + '<p><strong>Server:</strong> ' + @@SERVERNAME + '</p>';
        SET @EmailBody = @EmailBody + '<p><strong>Threshold:</strong> ' + 
                        CAST(@ThresholdMinutes AS VARCHAR(10)) + ' minutes</p>';
        SET @EmailBody = @EmailBody + '<p><strong>Total Queries:</strong> ' + 
                        CAST(@QueryCount AS VARCHAR(10)) + '</p>';
        
        SET @EmailBody = @EmailBody + '<table border="1" cellpadding="5" cellspacing="0">';
        SET @EmailBody = @EmailBody + '<tr style="background-color:#f0f0f0;">';
        SET @EmailBody = @EmailBody + '<th>Session ID</th><th>User</th><th>Duration (min)</th>';
        SET @EmailBody = @EmailBody + '<th>CPU Time (ms)</th><th>Reads</th><th>Program</th>';
        SET @EmailBody = @EmailBody + '<th>Blocking Session</th><th>SQL Text</th></tr>';
        
        DECLARE @SessionID INT, @LoginName NVARCHAR(128), @HostName NVARCHAR(128);
        DECLARE @ProgramName NVARCHAR(128), @Duration DECIMAL(10,2), @CPUTime BIGINT;
        DECLARE @Reads BIGINT, @Writes BIGINT, @SQLText NVARCHAR(MAX), @BlockingSession INT;
        
        DECLARE query_cursor CURSOR FOR
        SELECT session_id, login_name, host_name, program_name, duration_minutes,
               cpu_time_ms, reads, writes, sql_text, blocking_session_id
        FROM #LongRunningQueries;
        
        OPEN query_cursor;
        FETCH NEXT FROM query_cursor INTO @SessionID, @LoginName, @HostName, @ProgramName,
              @Duration, @CPUTime, @Reads, @Writes, @SQLText, @BlockingSession;
        
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @EmailBody = @EmailBody + '<tr>';
            SET @EmailBody = @EmailBody + '<td>' + CAST(@SessionID AS VARCHAR(10)) + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + ISNULL(@LoginName, 'N/A') + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + CAST(@Duration AS VARCHAR(20)) + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + CAST(@CPUTime AS VARCHAR(20)) + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + CAST(@Reads AS VARCHAR(20)) + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + SUBSTRING(ISNULL(@ProgramName, 'N/A'), 1, 30) + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + ISNULL(CAST(@BlockingSession AS VARCHAR(10)), 'None') + '</td>';
            SET @EmailBody = @EmailBody + '<td>' + SUBSTRING(@SQLText, 1, 200) + '...</td>';
            SET @EmailBody = @EmailBody + '</tr>';
            
            FETCH NEXT FROM query_cursor INTO @SessionID, @LoginName, @HostName, @ProgramName,
                  @Duration, @CPUTime, @Reads, @Writes, @SQLText, @BlockingSession;
        END;
        
        CLOSE query_cursor;
        DEALLOCATE query_cursor;
        
        SET @EmailBody = @EmailBody + '</table></body></html>';
        
        -- Send email
        EXEC msdb.dbo.sp_send_dbmail
            @profile_name = 'DatabaseAlerts',
            @recipients = 'dba-team@yourcompany.com;support-team@yourcompany.com',
            @subject = @Subject,
            @body = @EmailBody,
            @body_format = 'HTML';
        
        -- Log the alert
        INSERT INTO QueryAlertLog (AlertTime, QueryCount, ThresholdMinutes, EmailSent)
        VALUES (GETDATE(), @QueryCount, @ThresholdMinutes, 'Y');
    END;
    
    DROP TABLE #LongRunningQueries;
END;
```

### Creating SQL Agent Job

```sql
-- Create the job
EXEC msdb.dbo.sp_add_job
    @job_name = N'Long Running Query Monitor',
    @description = N'Monitors and alerts on long running queries';

-- Add job step
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'Long Running Query Monitor',
    @step_name = N'Check Long Running Queries',
    @command = N'EXEC sp_MonitorLongRunningQueries',
    @database_name = N'YourDatabase';

-- Create schedule (every 5 minutes)
EXEC msdb.dbo.sp_add_schedule
    @schedule_name = N'Every 5 Minutes',
    @freq_type = 4,
    @freq_interval = 1,
    @freq_subday_type = 4,
    @freq_subday_interval = 5;

-- Attach schedule to job
EXEC msdb.dbo.sp_attach_schedule
    @job_name = N'Long Running Query Monitor',
    @schedule_name = N'Every 5 Minutes';

-- Add job to local server
EXEC msdb.dbo.sp_add_jobserver
    @job_name = N'Long Running Query Monitor',
    @server_name = N'(local)';
```

## PostgreSQL Implementation

PostgreSQL can send emails through external utilities or extensions like `pg_mail`.

### Installing pg_mail Extension (if available)

```sql
-- Install pg_mail extension (requires superuser)
CREATE EXTENSION IF NOT EXISTS pg_mail;
```

### Alternative: Using psql and external mail utility

Create a shell script for email notifications:

```bash
#!/bin/bash
# File: /opt/scripts/send_alert_email.sh

SUBJECT="$1"
BODY="$2"
RECIPIENTS="dba-team@yourcompany.com,support-team@yourcompany.com"

# Using sendmail
echo -e "Subject: $SUBJECT\nContent-Type: text/html\n\n$BODY" | sendmail $RECIPIENTS

# Alternative: Using mail command
# echo "$BODY" | mail -s "$SUBJECT" -a "Content-Type: text/html" $RECIPIENTS
```

### PostgreSQL Monitoring Function

```sql
-- Create function to detect long running queries
CREATE OR REPLACE FUNCTION monitor_long_running_queries()
RETURNS TABLE(
    pid INT,
    usename TEXT,
    application_name TEXT,
    client_addr INET,
    duration INTERVAL,
    state TEXT,
    query_text TEXT
) AS $$
DECLARE
    threshold_minutes INTEGER := 5;
    query_count INTEGER := 0;
    email_body TEXT := '';
    query_rec RECORD;
BEGIN
    -- Find long running queries
    FOR query_rec IN
        SELECT 
            pg_stat_activity.pid,
            pg_stat_activity.usename,
            pg_stat_activity.application_name,
            pg_stat_activity.client_addr,
            NOW() - pg_stat_activity.query_start AS duration,
            pg_stat_activity.state,
            pg_stat_activity.query AS query_text
        FROM pg_stat_activity
        WHERE pg_stat_activity.state = 'active'
        AND pg_stat_activity.query_start IS NOT NULL
        AND NOW() - pg_stat_activity.query_start > INTERVAL '5 minutes'
        AND pg_stat_activity.pid != pg_backend_pid()
        ORDER BY duration DESC
    LOOP
        query_count := query_count + 1;
        
        -- Return the query info
        pid := query_rec.pid;
        usename := query_rec.usename;
        application_name := query_rec.application_name;
        client_addr := query_rec.client_addr;
        duration := query_rec.duration;
        state := query_rec.state;
        query_text := query_rec.query_text;
        
        RETURN NEXT;
    END LOOP;
    
    -- If queries found, prepare email
    IF query_count > 0 THEN
        email_body := '<html><body>';
        email_body := email_body || '<h2>Long Running Query Alert - ' || NOW()::TEXT || '</h2>';
        email_body := email_body || '<p><strong>Server:</strong> ' || current_setting('cluster_name') || '</p>';
        email_body := email_body || '<p><strong>Database:</strong> ' || current_database() || '</p>';
        email_body := email_body || '<p><strong>Total Queries:</strong> ' || query_count || '</p>';
        
        -- Call external script to send email
        PERFORM pg_notify('long_query_alert', email_body);
    END IF;
END;
$$ LANGUAGE plpgsql;
```

### Creating a Monitoring Daemon (Python Script)

```python
#!/usr/bin/env python3
# File: /opt/scripts/pg_long_query_monitor.py

import psycopg2
import smtplib
import time
import logging
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

# Configuration
DB_CONFIG = {
    'host': 'localhost',
    'database': 'your_database',
    'user': 'monitoring_user',
    'password': 'your_password'
}

SMTP_CONFIG = {
    'server': 'smtp.yourcompany.com',
    'port': 587,
    'username': 'alerts@yourcompany.com',
    'password': 'your_smtp_password',
    'sender': 'database-alerts@yourcompany.com'
}

RECIPIENTS = ['dba-team@yourcompany.com', 'support-team@yourcompany.com']
CHECK_INTERVAL = 300  # 5 minutes

def send_email(subject, body):
    try:
        msg = MIMEMultipart()
        msg['From'] = SMTP_CONFIG['sender']
        msg['To'] = ', '.join(RECIPIENTS)
        msg['Subject'] = subject
        
        msg.attach(MIMEText(body, 'html'))
        
        server = smtplib.SMTP(SMTP_CONFIG['server'], SMTP_CONFIG['port'])
        server.starttls()
        server.login(SMTP_CONFIG['username'], SMTP_CONFIG['password'])
        server.send_message(msg)
        server.quit()
        
        logging.info(f"Alert email sent: {subject}")
        
    except Exception as e:
        logging.error(f"Failed to send email: {e}")

def check_long_running_queries():
    try:
        conn = psycopg2.connect(**DB_CONFIG)
        cur = conn.cursor()
        
        query = """
        SELECT 
            pid,
            usename,
            application_name,
            client_addr,
            EXTRACT(EPOCH FROM (NOW() - query_start))/60 AS duration_minutes,
            state,
            LEFT(query, 200) AS query_text
        FROM pg_stat_activity
        WHERE state = 'active'
        AND query_start IS NOT NULL
        AND NOW() - query_start > INTERVAL '5 minutes'
        AND pid != pg_backend_pid()
        ORDER BY duration_minutes DESC;
        """
        
        cur.execute(query)
        results = cur.fetchall()
        
        if results:
            subject = f"ALERT: {len(results)} Long Running Queries on {DB_CONFIG['host']}"
            
            body = f"""
            <html><body>
            <h2>Long Running Query Alert - {datetime.now()}</h2>
            <p><strong>Server:</strong> {DB_CONFIG['host']}</p>
            <p><strong>Database:</strong> {DB_CONFIG['database']}</p>
            <p><strong>Total Queries:</strong> {len(results)}</p>
            
            <table border="1" cellpadding="5" cellspacing="0">
            <tr style="background-color:#f0f0f0;">
                <th>PID</th><th>User</th><th>Duration (min)</th>
                <th>Application</th><th>Client</th><th>Query</th>
            </tr>
            """
            
            for row in results:
                body += f"""
                <tr>
                    <td>{row[0]}</td>
                    <td>{row[1] or 'N/A'}</td>
                    <td>{row[4]:.1f}</td>
                    <td>{row[2] or 'N/A'}</td>
                    <td>{row[3] or 'N/A'}</td>
                    <td>{row[6]}...</td>
                </tr>
                """
            
            body += "</table></body></html>"
            
            send_email(subject, body)
        
        cur.close()
        conn.close()
        
    except Exception as e:
        logging.error(f"Database check failed: {e}")

def main():
    logging.basicConfig(level=logging.INFO, 
                       format='%(asctime)s - %(levelname)s - %(message)s')
    
    logging.info("Starting PostgreSQL long query monitor")
    
    while True:
        check_long_running_queries()
        time.sleep(CHECK_INTERVAL)

if __name__ == "__main__":
    main()
```

## MySQL Implementation

MySQL doesn't have built-in email functionality, but we can use external tools or UDF functions.

### Creating the Monitoring Procedure

```sql
DELIMITER $$

CREATE PROCEDURE MonitorLongRunningQueries()
BEGIN
    DECLARE done INT DEFAULT FALSE;
    DECLARE v_id BIGINT;
    DECLARE v_user VARCHAR(32);
    DECLARE v_host VARCHAR(64);
    DECLARE v_db VARCHAR(64);
    DECLARE v_command VARCHAR(16);
    DECLARE v_time INT;
    DECLARE v_info LONGTEXT;
    DECLARE v_count INT DEFAULT 0;
    DECLARE v_threshold INT DEFAULT 300; -- 5 minutes
    
    DECLARE query_cursor CURSOR FOR
        SELECT id, user, host, db, command, time, info
        FROM information_schema.processlist
        WHERE command IN ('Query', 'Execute')
        AND time > v_threshold
        AND id != CONNECTION_ID()
        ORDER BY time DESC;
        
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
    
    -- Create temporary table for results
    DROP TEMPORARY TABLE IF EXISTS temp_long_queries;
    CREATE TEMPORARY TABLE temp_long_queries (
        process_id BIGINT,
        user_name VARCHAR(32),
        host_name VARCHAR(64),
        database_name VARCHAR(64),
        command_type VARCHAR(16),
        duration_seconds INT,
        query_text LONGTEXT
    );
    
    OPEN query_cursor;
    
    query_loop: LOOP
        FETCH query_cursor INTO v_id, v_user, v_host, v_db, v_command, v_time, v_info;
        IF done THEN
            LEAVE query_loop;
        END IF;
        
        INSERT INTO temp_long_queries VALUES (
            v_id, v_user, v_host, v_db, v_command, v_time, 
            SUBSTRING(COALESCE(v_info, 'N/A'), 1, 500)
        );
        SET v_count = v_count + 1;
        
    END LOOP;
    
    CLOSE query_cursor;
    
    -- If long running queries found, log and trigger external alert
    IF v_count > 0 THEN
        INSERT INTO query_alert_log (
            alert_time, query_count, threshold_seconds, server_name
        ) VALUES (
            NOW(), v_count, v_threshold, @@hostname
        );
        
        -- Write alert file for external script to process
        SELECT CONCAT(
            'ALERT: ', v_count, ' long running queries on ', @@hostname, ' at ', NOW()
        ) INTO OUTFILE '/tmp/mysql_alert_flag.txt';
    END IF;
    
    DROP TEMPORARY TABLE temp_long_queries;
END$$

DELIMITER ;
```

### External Python Monitor for MySQL

```python
#!/usr/bin/env python3
# File: /opt/scripts/mysql_long_query_monitor.py

import mysql.connector
import smtplib
import time
import os
import logging
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

# Configuration (similar to PostgreSQL example)
DB_CONFIG = {
    'host': 'localhost',
    'database': 'your_database',
    'user': 'monitoring_user',
    'password': 'your_password'
}

def check_mysql_long_queries():
    try:
        conn = mysql.connector.connect(**DB_CONFIG)
        cursor = conn.cursor()
        
        # Call the monitoring procedure
        cursor.callproc('MonitorLongRunningQueries')
        
        # Check for alert flag file
        if os.path.exists('/tmp/mysql_alert_flag.txt'):
            # Get detailed query information
            cursor.execute("""
                SELECT process_id, user_name, host_name, database_name,
                       duration_seconds, query_text
                FROM temp_long_queries
                ORDER BY duration_seconds DESC
            """)
            
            results = cursor.fetchall()
            
            if results:
                send_mysql_alert(results)
            
            # Remove flag file
            os.remove('/tmp/mysql_alert_flag.txt')
        
        cursor.close()
        conn.close()
        
    except Exception as e:
        logging.error(f"MySQL check failed: {e}")

def send_mysql_alert(query_data):
    subject = f"ALERT: {len(query_data)} Long Running Queries on MySQL"
    
    body = f"""
    <html><body>
    <h2>MySQL Long Running Query Alert - {datetime.now()}</h2>
    <p><strong>Server:</strong> {DB_CONFIG['host']}</p>
    <p><strong>Total Queries:</strong> {len(query_data)}</p>
    
    <table border="1" cellpadding="5" cellspacing="0">
    <tr style="background-color:#f0f0f0;">
        <th>Process ID</th><th>User</th><th>Host</th><th>Database</th>
        <th>Duration (sec)</th><th>Query</th>
    </tr>
    """
    
    for row in query_data:
        body += f"""
        <tr>
            <td>{row[0]}</td>
            <td>{row[1]}</td>
            <td>{row[2]}</td>
            <td>{row[3] or 'N/A'}</td>
            <td>{row[4]}</td>
            <td>{row[5][:100]}...</td>
        </tr>
        """
    
    body += "</table></body></html>"
    
    # Use same email sending logic as previous examples
    send_email(subject, body)
```

## Best Practices

### 1. **Set Appropriate Thresholds**

```sql
-- Different thresholds for different environments
-- Production: 2-5 minutes
-- Development: 10-15 minutes
-- Reporting: 30+ minutes
```

### 2. **Implement Rate Limiting**

```sql
-- Oracle example: Prevent spam alerts
CREATE TABLE alert_throttle (
    alert_type VARCHAR2(50),
    last_sent_time DATE,
    send_count NUMBER DEFAULT 0
);

-- Only send alerts every 30 minutes maximum
IF (SYSDATE - last_alert_time) * 24 * 60 < 30 THEN
    RETURN; -- Skip sending
END IF;
```

### 3. **Include Actionable Information**

Make sure your alerts include:
- **Session/Process IDs** for termination if needed
- **User and application** context
- **Query text** (truncated for readability)
- **Resource consumption** (CPU, I/O, memory)
- **Blocking information** if applicable

### 4. **Create Alert Severity Levels**

```sql
-- Different alert levels based on duration
CASE 
    WHEN duration_minutes > 60 THEN 'CRITICAL'
    WHEN duration_minutes > 30 THEN 'HIGH'
    WHEN duration_minutes > 10 THEN 'MEDIUM'
    ELSE 'LOW'
END as severity_level
```

### 5. **Log All Alerts**

Always maintain an audit trail:

```sql
CREATE TABLE comprehensive_alert_log (
    alert_id NUMBER PRIMARY KEY,
    alert_time TIMESTAMP,
    server_name VARCHAR2(100),
    database_name VARCHAR2(100),
    query_count NUMBER,
    threshold_seconds NUMBER,
    severity_level VARCHAR2(20),
    email_sent CHAR(1),
    recipients VARCHAR2(500),
    error_message VARCHAR2(4000),
    created_by VARCHAR2(100) DEFAULT USER
);
```

## Troubleshooting Common Issues

### Email Configuration Problems

**Oracle UTL_MAIL Issues:**
```sql
-- Check SMTP configuration
SELECT name, value FROM v$parameter WHERE name LIKE '%smtp%';

-- Test email sending
BEGIN
    UTL_MAIL.send(
        sender => 'test@yourcompany.com',
        recipients => 'admin@yourcompany.com',
        subject => 'Test Email',
        message => 'This is a test message'
    );
END;
/
```

**SQL Server Database Mail Issues:**
```sql
-- Check Database Mail status
SELECT * FROM msdb.dbo.sysmail_event_log ORDER BY log_date DESC;

-- Check mail queue
SELECT * FROM msdb.dbo.sysmail_mailitems ORDER BY send_request_date DESC;

-- Test mail configuration
EXEC msdb.dbo.sp_send_dbmail
    @profile_name = 'DatabaseAlerts',
    @recipients = 'test@yourcompany.com',
    @subject = 'Test Email',
    @body = 'This is a test message';
```

### Performance Impact Considerations

Monitor the monitoring system itself:

```sql
-- Oracle: Check job execution history
SELECT job_name, log_date, status, error#, additional_info
FROM user_scheduler_job_log
WHERE job_name = 'LONG_QUERY_MONITOR_JOB'
ORDER BY log_date DESC;

-- SQL Server: Check job history
SELECT 
    h.step_name,
    h.run_date,
    h.run_time,
    h.run_duration,
    h.run_status,
    h.message
FROM msdb.dbo.sysjobhistory h
JOIN msdb.dbo.sysjobs j ON h.job_id = j.job_id
WHERE j.name = 'Long Running Query Monitor'
ORDER BY h.run_date DESC, h.run_time DESC;
```

### False Positive Reduction

Exclude legitimate long-running operations:

```sql
-- Oracle: Exclude known batch jobs
WHERE s.program NOT IN ('batch_process.exe', 'etl_loader.exe')
AND s.username NOT IN ('BATCH_USER', 'ETL_USER')

-- SQL Server: Exclude backup operations
WHERE r.command NOT IN ('BACKUP DATABASE', 'BACKUP LOG')
AND s.program_name NOT LIKE '%SQL Server Management Studio%'
```

## Conclusion

Implementing automated email notifications for long-running database queries is a crucial component of proactive database monitoring. The solutions presented here provide:

### Key Benefits:
- **Immediate Awareness**: Support teams get real-time notifications about performance issues
- **Reduced Downtime**: Faster response times prevent minor issues from becoming major outages
- **Historical Tracking**: Alert logs provide valuable data for performance analysis
- **Customizable Thresholds**: Adapt monitoring to your specific environment needs

### Implementation Checklist:
1. ✅ **Configure email infrastructure** (SMTP settings, accounts, profiles)
2. ✅ **Create monitoring procedures** with appropriate query logic
3. ✅ **Set up scheduling** (database jobs, cron jobs, or services)
4. ✅ **Implement logging** for audit trails and troubleshooting
5. ✅ **Test thoroughly** in non-production environments
6. ✅ **Fine-tune thresholds** to minimize false positives
7. ✅ **Document procedures** for support team reference

### Next Steps:
Consider enhancing these basic implementations with:
- **Integration with monitoring tools** (Nagios, Zabbix, Prometheus)
- **Slack/Teams notifications** in addition to email
- **Automated query termination** for extreme cases
- **Machine learning** to predict and prevent performance issues
- **Dashboard integration** for real-time visualization

Remember: The goal isn't just to detect problems, but to provide actionable information that helps your support team resolve issues quickly and effectively. Start simple, monitor the effectiveness of your alerts, and iterate based on your team's needs and feedback.

By implementing these database email notification systems, you're taking a significant step toward proactive database administration and improved system reliability. Your support team (and your users) will thank you for the early warning system that helps prevent small performance hiccups from becoming major incidents.

---

*Have you implemented similar monitoring solutions? Share your experiences and improvements in the comments below!*
