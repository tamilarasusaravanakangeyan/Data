# Implementing IP Whitelisting in Oracle Database

Restricting database access to only trusted IP addresses is a critical security measure. Oracle Database does not provide native IP whitelisting at the database level, but you can achieve this using a combination of network, database, and OS-level controls.

## Components Needed for IP Whitelisting

1. **Network Firewall or Security Group**
   - Restricts incoming connections to the database server based on source IP addresses.
2. **Oracle Listener Configuration**
   - Controls which IPs can connect to the listener using TCP Valid Node Checking.
3. **Database Logon Trigger**
   - Enforces IP checks at the session level and disconnects unauthorized users.
4. **Network ACLs (Access Control Lists)**
   - Used for outbound connections, not for inbound whitelisting.
5. **Audit and Logging**
   - Tracks connection attempts and rejected sessions for compliance.

## Key Technique: TCP Valid Node Checking (Listener Level)

Oracle Listener can restrict connections using the `tcp.validnode_checking` parameter in `sqlnet.ora`.

### Example: Listener IP Whitelisting

Edit your `sqlnet.ora` file (usually in `$ORACLE_HOME/network/admin`):

```ini
TCP.VALIDNODE_CHECKING = YES
TCP.INVITED_NODES = (192.168.1.10, 10.0.0.5, 203.0.113.42)
TCP.EXCLUDED_NODES = (0.0.0.0)
```
- **TCP.INVITED_NODES**: List of allowed IP addresses
- **TCP.EXCLUDED_NODES**: List of explicitly denied IPs

Restart the listener after making changes:
```sh
lsnrctl reload
```

## Key Technique: Database Logon Trigger (Session Level)

Add an extra layer by creating a logon trigger that checks the client's IP address and disconnects unauthorized users.

### Example: Logon Trigger for IP Whitelisting

```sql
CREATE OR REPLACE TRIGGER ip_whitelist_logon
AFTER LOGON ON DATABASE
DECLARE
    v_ip VARCHAR2(50);
    v_allowed_ips SYS.ODCIVARCHAR2LIST := SYS.ODCIVARCHAR2LIST('192.168.1.10', '10.0.0.5', '203.0.113.42');
BEGIN
    -- Get client IP address
    SELECT SYS_CONTEXT('USERENV', 'IP_ADDRESS') INTO v_ip FROM dual;
    
    -- If not in allowed list, disconnect
    IF v_ip IS NULL OR NOT v_ip IN (SELECT COLUMN_VALUE FROM TABLE(v_allowed_ips)) THEN
        RAISE_APPLICATION_ERROR(-20001, 'Access denied: IP not whitelisted.');
    END IF;
END;
/
```
- **SYS_CONTEXT('USERENV', 'IP_ADDRESS')**: Retrieves the client's IP address
- **ODCIVARCHAR2LIST**: PL/SQL collection for allowed IPs
- **RAISE_APPLICATION_ERROR**: Immediately disconnects unauthorized sessions

## Key Technique: Network Firewall/Security Group

Configure your OS firewall (iptables, firewalld, Windows Firewall) or cloud security group to allow only whitelisted IPs to connect to the database port (default 1521).

### Example: Linux iptables Rule
```sh
# Allow only specific IPs to connect to Oracle port 1521
iptables -A INPUT -p tcp --dport 1521 -s 192.168.1.10 -j ACCEPT
iptables -A INPUT -p tcp --dport 1521 -s 10.0.0.5 -j ACCEPT
iptables -A INPUT -p tcp --dport 1521 -s 203.0.113.42 -j ACCEPT
iptables -A INPUT -p tcp --dport 1521 -j DROP
```

## Audit and Logging

Enable auditing to track connection attempts and rejected sessions:
```sql
AUDIT SESSION;
```
Check audit logs for denied connections:
```sql
SELECT * FROM DBA_AUDIT_SESSION WHERE RETURNCODE = 20001;
```

## Summary Table

| Component         | Purpose                                 | Example/Config                |
|------------------|-----------------------------------------|-------------------------------|
| Firewall         | Block unauthorized IPs at network level  | iptables, security group      |
| Listener         | Allow only whitelisted IPs to connect    | sqlnet.ora TCP.VALIDNODE_CHECKING |
| Logon Trigger    | Enforce IP check at session start        | PL/SQL trigger                |
| Audit/Logging    | Track and report access attempts         | AUDIT SESSION                 |

## Best Practice
- Use multiple layers (firewall, listener, trigger) for defense in depth
- Regularly review and update whitelisted IPs
- Monitor audit logs for suspicious activity
- Test changes in a non-production environment first

---

*This guide helps you implement robust IP whitelisting for Oracle Database, protecting your data from unauthorized access.*
