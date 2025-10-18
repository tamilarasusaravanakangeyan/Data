# Implementing Enterprise Integration Patterns with Apache Camel: Salesforce to Oracle Integration

Enterprise Integration Patterns (EIP) provide proven solutions for integrating disparate systems in a scalable, maintainable way. In this post, we’ll walk through a real-world scenario: reading data from Salesforce and storing it in an Oracle database using Apache Camel, Java Spring Boot, and DTO mapping.

## Solution Overview

- **Integration Framework:** Apache Camel (Route DSL)
- **Source:** Salesforce (using Camel Salesforce component)
- **Target:** Oracle Database
- **Application Framework:** Java Spring Boot
- **Data Mapping:** Salesforce DTO ↔ User-defined DTO (Oracle schema)
- **Scheduling:** Route runs every 5 minutes
- **Build Tool:** Maven (with DTO generator plugin)

## Architecture

1. **Polling:** Camel route triggers every 5 minutes.
2. **Source Adapter:** Reads data from Salesforce using Camel’s Salesforce component.
3. **Transformation:** Maps Salesforce DTOs to downstream DTOs (matching Oracle schema).
4. **Target Adapter:** Persists data into Oracle using Camel’s JDBC or JPA component.

## Implementation Steps

### 1. Maven Setup

Add dependencies for Camel, Salesforce, Oracle, and DTO generator in your `pom.xml`:

```xml
<dependency>
		<groupId>org.apache.camel.springboot</groupId>
		<artifactId>camel-spring-boot-starter</artifactId>
</dependency>
<dependency>
		<groupId>org.apache.camel.springboot</groupId>
		<artifactId>camel-salesforce-starter</artifactId>
</dependency>
<dependency>
		<groupId>com.oracle.database.jdbc</groupId>
		<artifactId>ojdbc8</artifactId>
</dependency>
```

#### Sample DTO Generator Plugin

Use the [camel-salesforce-maven-plugin](https://camel.apache.org/components/3.20.x/camel-salesforce/camel-salesforce-maven-plugin.html) to generate DTOs from Salesforce schema:

```xml
<plugin>
	<groupId>org.apache.camel.maven</groupId>
	<artifactId>camel-salesforce-maven-plugin</artifactId>
	<version>3.20.2</version>
	<executions>
		<execution>
			<goals>
				<goal>generate</goal>
			</goals>
		</execution>
	</executions>
	<configuration>
		<clientId>${salesforce.clientId}</clientId>
		<clientSecret>${salesforce.clientSecret}</clientSecret>
		<userName>${salesforce.userName}</userName>
		<password>${salesforce.password}</password>
		<loginUrl>https://login.salesforce.com</loginUrl>
		<packageName>com.example.salesforce.dto</packageName>
		<!-- useStringsForPicklists casts Salesforce enum picklists as String in DTOs -->
		<useStringsForPicklists>true</useStringsForPicklists>
	</configuration>
</plugin>
```

**Note:** The `<useStringsForPicklists>true</useStringsForPicklists>` option ensures that Salesforce picklist (enum) fields are generated as `String` in the DTOs, simplifying type casting and mapping to downstream systems.

### 2. DTO Generation

Use the plugin above to generate Java DTOs for Salesforce objects and your Oracle schema.

- **Salesforce DTO:** Generated from Salesforce object schema.
- **Downstream DTO:** User-defined, matching Oracle table structure.

### 3. Camel Route DSL

Define a Camel route that:

- Triggers every 5 minutes (`quartz` or `timer` component).
- Reads from Salesforce.
- Maps data to downstream DTO.
- Writes to Oracle.

Example (Java DSL):

```java
from("quartz://salesforcePoller?cron=0+0/5+*+*+*+?")
		.routeId("SalesforceToOracleRoute")
		.to("salesforce:query?sObjectQuery=SELECT+Id,Name+FROM+Account")
		.process(exchange -> {
				// Map Salesforce DTO to Downstream DTO
				SalesforceAccount sfAccount = exchange.getIn().getBody(SalesforceAccount.class);
				OracleAccountDto oracleDto = mapToOracleDto(sfAccount);
				exchange.getIn().setBody(oracleDto);
		})
		.to("jdbc:oracleDataSource?useHeadersAsParameters=true");
```

### 4. Data Mapping

Implement a mapper class to convert Salesforce DTOs to your Oracle DTOs:

```java
public OracleAccountDto mapToOracleDto(SalesforceAccount sfAccount) {
		OracleAccountDto dto = new OracleAccountDto();
		dto.setId(sfAccount.getId());
		dto.setName(sfAccount.getName());
		// ...map other fields...
		return dto;
}
```

### 5. Spring Boot Configuration

Configure Salesforce and Oracle connections in `application.properties`:

```properties
camel.component.salesforce.client-id=...
camel.component.salesforce.client-secret=...
camel.component.salesforce.user-name=...
camel.component.salesforce.password=...
spring.datasource.url=jdbc:oracle:thin:@//host:port/service
spring.datasource.username=...
spring.datasource.password=...
```

### 6. Running the Integration

- Build with Maven: `mvn clean install`
- Run the Spring Boot app: `java -jar target/your-app.jar`
- The route will poll Salesforce every 5 minutes and update Oracle.

## Conclusion

This approach leverages Apache Camel’s EIP support, Spring Boot’s rapid development, and DTO mapping for clean, maintainable integration between Salesforce and Oracle. The pattern is extensible for other sources/targets and can be enhanced with error handling, monitoring, and more.
