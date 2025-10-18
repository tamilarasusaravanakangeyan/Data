# Oracle Database Unique Features

This repository demonstrates Oracle Database's unique security and data management features with practical examples. These features provide enterprise-level data protection and management capabilities that are often challenging to implement in other database systems.

## Overview

| Feature | Row Level? | Column Level? | App Change Needed? | Example Use Case |
|---------|------------|---------------|-------------------|------------------|
| VPD (Predicate) | Yes | Yes | No | Hide rows for unauthorized users |
| Data Redaction | No | Yes | No | Mask salary for most users |
| In-Database Archiving | Yes | No | Minor (archival) | Hide soft-deleted records |

## 1. Virtual Private Database (VPD)

VPD automatically enforces row-level and column-level security by adding predicates to SQL queries transparently.

### Example Implementation

```sql
-- Create sample table
CREATE TABLE employees (
    emp_id NUMBER PRIMARY KEY,
    name VARCHAR2(100),
    department VARCHAR2(50),
    salary NUMBER,
    manager_id NUMBER
);

-- Insert sample data
INSERT INTO employees VALUES (1, 'John Doe', 'HR', 75000, NULL);
INSERT INTO employees VALUES (2, 'Jane Smith', 'IT', 85000, 1);
INSERT INTO employees VALUES (3, 'Bob Johnson', 'Finance', 90000, 1);
INSERT INTO employees VALUES (4, 'Alice Brown', 'IT', 78000, 2);

-- Create security context function
CREATE OR REPLACE FUNCTION emp_security_policy(
    schema_var IN VARCHAR2,
    table_var IN VARCHAR2
) RETURN VARCHAR2 AS
    predicate VARCHAR2(400);
BEGIN
    -- Managers can see all employees in their department
    -- Regular employees can only see their own record
    IF SYS_CONTEXT('USERENV', 'SESSION_USER') = 'MANAGER_USER' THEN
        predicate := 'department = SYS_CONTEXT(''USERENV'', ''CLIENT_IDENTIFIER'')';
    ELSE
        predicate := 'emp_id = SYS_CONTEXT(''USERENV'', ''CLIENT_IDENTIFIER'')';
    END IF;
    
    RETURN predicate;
END;
/

-- Apply VPD policy
BEGIN
    DBMS_RLS.ADD_POLICY(
        object_schema   => 'HR_SCHEMA',
        object_name     => 'employees',
        policy_name     => 'emp_access_policy',
        function_schema => 'HR_SCHEMA',
        policy_function => 'emp_security_policy',
        statement_types => 'SELECT, INSERT, UPDATE, DELETE'
    );
END;
/

-- Set user context (typically done at login)
EXEC DBMS_SESSION.SET_IDENTIFIER('IT'); -- Department for manager
-- or
EXEC DBMS_SESSION.SET_IDENTIFIER('2'); -- Employee ID for regular user

-- Query will automatically filter based on user context
SELECT * FROM employees; -- Only shows authorized rows
```

### VPD Benefits

- **Transparent**: No application code changes required
- **Comprehensive**: Works with all DML operations
- **Flexible**: Complex business logic can be implemented
- **Secure**: Cannot be bypassed by application bugs

## 2. Oracle Data Redaction

Dynamically masks sensitive data at query time without modifying the underlying data.

### Data Redaction Implementation

```sql
-- Create table with sensitive data
CREATE TABLE customer_data (
    customer_id NUMBER PRIMARY KEY,
    name VARCHAR2(100),
    ssn VARCHAR2(11),
    credit_card VARCHAR2(16),
    salary NUMBER,
    email VARCHAR2(100)
);

-- Insert sample data
INSERT INTO customer_data VALUES (
    1, 'John Smith', '123-45-6789', '4532123456789012', 125000, 'john@email.com'
);
INSERT INTO customer_data VALUES (
    2, 'Jane Doe', '987-65-4321', '5555444433332222', 95000, 'jane@email.com'
);

-- Apply different redaction policies

-- 1. Full redaction for SSN (replace with X's)
BEGIN
    DBMS_REDACT.ADD_POLICY(
        object_schema   => 'CUSTOMER_SCHEMA',
        object_name     => 'customer_data', 
        column_name     => 'ssn',
        policy_name     => 'ssn_redaction_policy',
        function_type   => DBMS_REDACT.FULL,
        expression      => 'SYS_CONTEXT(''USERENV'', ''SESSION_USER'') != ''ADMIN_USER'''
    );
END;
/

-- 2. Partial redaction for credit card (show only last 4 digits)
BEGIN
    DBMS_REDACT.ADD_POLICY(
        object_schema     => 'CUSTOMER_SCHEMA',
        object_name       => 'customer_data',
        column_name       => 'credit_card',
        policy_name       => 'cc_redaction_policy', 
        function_type     => DBMS_REDACT.PARTIAL,
        function_parameters => 'VVVVVVVVVVVVFFF,VVVV-VVVV-VVVV-,*,1,12',
        expression        => 'SYS_CONTEXT(''USERENV'', ''SESSION_USER'') NOT IN (''ADMIN_USER'', ''FINANCE_USER'')'
    );
END;
/

-- 3. Random redaction for salary (show random values in range)
BEGIN
    DBMS_REDACT.ADD_POLICY(
        object_schema   => 'CUSTOMER_SCHEMA',
        object_name     => 'customer_data',
        column_name     => 'salary',
        policy_name     => 'salary_redaction_policy',
        function_type   => DBMS_REDACT.RANDOM,
        expression      => 'SYS_CONTEXT(''USERENV'', ''SESSION_USER'') NOT IN (''ADMIN_USER'', ''HR_USER'')'
    );
END;
/

-- 4. Regular expression redaction for email (mask domain)
BEGIN
    DBMS_REDACT.ADD_POLICY(
        object_schema       => 'CUSTOMER_SCHEMA',
        object_name         => 'customer_data',
        column_name         => 'email',
        policy_name         => 'email_redaction_policy',
        function_type       => DBMS_REDACT.REGEXP,
        regexp_pattern      => '(@)(.*)(\.)(.*)$',
        regexp_replace_string => '@*****.com',
        expression          => 'SYS_CONTEXT(''USERENV'', ''SESSION_USER'') != ''ADMIN_USER'''
    );
END;
/

-- Query results for different users:
-- Regular user sees:
SELECT * FROM customer_data;
/*
CUSTOMER_ID | NAME       | SSN         | CREDIT_CARD      | SALARY | EMAIL
1          | John Smith | XXXXXXXXXXX | ************9012 | 87432  | john@*****.com
2          | Jane Doe   | XXXXXXXXXXX | ************2222 | 64821  | jane@*****.com
*/

-- Admin user sees original data unchanged
```

### Data Redaction Use Cases

- **Compliance**: GDPR, HIPAA, PCI-DSS requirements
- **Development**: Provide realistic test data without exposing sensitive information
- **Analytics**: Allow data analysis while protecting individual privacy
- **Third-party access**: Share data with partners while maintaining security

## 3. In-Database Archiving

Allows logical deletion of data while maintaining physical storage for compliance and recovery.

### Archiving Implementation

```sql
-- Enable In-Database Archiving on table
CREATE TABLE orders (
    order_id NUMBER PRIMARY KEY,
    customer_id NUMBER,
    order_date DATE,
    amount NUMBER,
    status VARCHAR2(20)
) ROW ARCHIVAL;

-- Insert sample data
INSERT INTO orders VALUES (1, 101, SYSDATE-30, 1500, 'COMPLETED');
INSERT INTO orders VALUES (2, 102, SYSDATE-20, 2500, 'COMPLETED');
INSERT INTO orders VALUES (3, 103, SYSDATE-10, 750, 'CANCELLED');
INSERT INTO orders VALUES (4, 104, SYSDATE-5, 3200, 'PROCESSING');

-- Archive (soft delete) cancelled orders
UPDATE orders 
SET ORA_ARCHIVE_STATE = DBMS_ILM.ARCHIVESTATENAME(1)
WHERE status = 'CANCELLED';

-- Normal query automatically excludes archived rows
SELECT * FROM orders;
/*
ORDER_ID | CUSTOMER_ID | ORDER_DATE | AMOUNT | STATUS
1        | 101         | ...        | 1500   | COMPLETED
2        | 102         | ...        | 2500   | COMPLETED  
4        | 104         | ...        | 3200   | PROCESSING
*/

-- Query to see ALL rows (including archived)
SELECT * FROM orders ORA_ARCHIVE_STATE_ALL;
/*
ORDER_ID | CUSTOMER_ID | ORDER_DATE | AMOUNT | STATUS     | ORA_ARCHIVE_STATE
1        | 101         | ...        | 1500   | COMPLETED  | 0
2        | 102         | ...        | 2500   | COMPLETED  | 0
3        | 103         | ...        | 750    | CANCELLED  | 1
4        | 104         | ...        | 3200   | PROCESSING | 0
*/

-- Query only archived rows
SELECT * FROM orders ORA_ARCHIVE_STATE_ARCHIVED;
/*
ORDER_ID | CUSTOMER_ID | ORDER_DATE | AMOUNT | STATUS
3        | 103         | ...        | 750    | CANCELLED
*/

-- Unarchive (restore) a record
UPDATE orders 
SET ORA_ARCHIVE_STATE = DBMS_ILM.ARCHIVESTATENAME(0)
WHERE order_id = 3;

-- Create archival policy for automatic archiving
BEGIN
    DBMS_ILM.ADD_POLICY(
        object_name    => 'orders',
        policy_name    => 'auto_archive_policy',
        action_type    => DBMS_ILM.ARCHIVE,
        condition_type => DBMS_ILM.CONDITION_DAYS_SINCE_LAST_MODIFICATION,
        condition_days => 365
    );
END;
/
```

### Archiving Benefits

- **Compliance**: Meet data retention requirements without data loss
- **Performance**: Improve query performance by reducing active dataset size
- **Storage**: Compressed archived data takes less space
- **Recovery**: Easily restore archived data when needed

## Best Practices

### Security Considerations

1. **VPD**: Test policies thoroughly to prevent data leakage
2. **Redaction**: Ensure redaction patterns don't reveal sensitive patterns
3. **Archiving**: Implement proper access controls for archived data

### Performance Optimization

```sql
-- Create appropriate indexes for VPD predicates
CREATE INDEX idx_emp_dept ON employees(department);

-- Monitor redaction performance
SELECT * FROM V$REDACTION_STATISTICS;

-- Monitor archival effectiveness  
SELECT 
    table_name,
    active_rows,
    archived_rows,
    compression_ratio
FROM USER_ILMARCHIVEDOBJECTS;
```

### Monitoring and Maintenance

```sql
-- Check active VPD policies
SELECT * FROM DBA_POLICIES;

-- Monitor redaction policies
SELECT * FROM REDACTION_POLICIES;

-- Check archival statistics
SELECT * FROM DBA_ILMARCHIVEDOBJECTS;
```

## Getting Started

1. **Prerequisites**: Oracle Database 12c+ (some features require specific versions)
2. **Permissions**: Requires DBA privileges to create policies
3. **Testing**: Always test in non-production environment first
4. **Documentation**: Review Oracle documentation for version-specific features

## Resources

- [Oracle Database Security Guide](https://docs.oracle.com/en/database/oracle/oracle-database/21/dbseg/)
- [VPD Implementation Guide](https://docs.oracle.com/en/database/oracle/oracle-database/21/dbseg/using-oracle-vpd-to-control-data-access.html)
- [Data Redaction Guide](https://docs.oracle.com/en/database/oracle/oracle-database/21/asoag/oracle-data-redaction.html)
- [Information Lifecycle Management](https://docs.oracle.com/en/database/oracle/oracle-database/21/vldbg/ilm-strategy.html)
