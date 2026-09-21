## Billing and Collection Web Application Update

The recent update to the TMCWD billing and collection web application introduces a more complete and operational workflow for utility billing and collection processes. The application now integrates customer account management, reading records, tariff computation, billing history, and collection monitoring into a single system.

### What’s New Added to the Billing System

- Collections search and account monitoring for unpaid accounts
- Account-based billing history review and as-billed analysis
- Reading-based billing logic using current and previous readings
- Water rate and tariff computation based on classification and meter size
- Billing adjustment tools for usage, penalties, and correction entries
- Reading sheet and zone-book management for field meter operations
- Payment status tracking for unpaid, paid, overdue, and waived accounts
- Role-based access and secure session management for billing personnel

This update improves the efficiency and accuracy of the billing process and strengthens collection follow-up for the water district.

## 1. Overview

This document describes the recent update to the TMCWD billing and collection web application, focusing on the billing, collection, reading, and tariff modules. The application is built with ASP.NET Core MVC and is organized into layered components:

- Application layer: TMCWD/TMCWD.Application
- Business logic layer: TMCWD/TMCWD.Billing and related transaction classes
- Data access layer: TMCWD/TMCWD.Data
- Shared models: TMCWD/TMCWD.Model
- Supporting services: TMCWD/TMCWD.Services

The current implementation supports customer account lookup, unpaid bill aggregation, penalty tracking, reading-based billing history, and tariff computation through account classification and meter size.

---

## 2. Objective of the Update

The update aims to provide a more complete and operational billing and collection workflow for the water district, including:

- faster account search and collection review
- visibility of outstanding bills and penalties
- tracking of billing history per account
- support for reading-based billing calculations
- tariff logic based on water classification and meter size
- improved integration between billing, collections, and reading records

---

## 3. Application Architecture

### 3.1 Web Application Startup
The web app is initialized in the main startup file:

- TMCWD/TMCWD.Application/Program.cs

Important setup points include:

- dependency registration for billing-related transaction classes
- session configuration for user state
- JWT authentication configuration
- MVC routing and static asset setup

The application also registers the following major billing-related services:

- BillingTransaction
- PenaltyTransaction
- WaterRateTransaction
- ReadingSheetTransaction
- ZoneBookTransaction
- ReadingTransaction
- BillingAdjustmentTransaction
- OtherChargeTransaction

### 3.2 MVC Controllers
The main updates are handled through these controllers:

- TMCWD/TMCWD.Application/Controllers/BillingController.cs
- TMCWD/TMCWD.Application/Controllers/CollectionsController.cs

These controllers orchestrate requests between the user interface and the service/transaction layer.

---

## 4. Updated Billing Modules

### 4.1 Billing Management
The billing logic is implemented mainly through the BillingTransaction class:

- TMCWD/TMCWD.Billing/BillingTransaction.cs

This transaction supports:

- retrieving a billing record by ID
- retrieving all billing records
- retrieving billing records by account ID
- retrieving billing by reference number
- saving or updating billing entries
- retrieving billing history using account number

This enables the application to:

- review outstanding balances
- compare billing periods
- retrieve account-specific billing records
- maintain a reference-based billing history

### 4.2 Billing History and As-Billed Retrieval
The BillingController includes API-style endpoints for bill retrieval and analysis:

- History(accountNumber)
- GetAsBilledValues(accountNumber, billingDate)

These methods:

- look up the target customer account
- fetch associated billing records
- match billing records with stored readings
- compute present/previous readings and usage
- calculate current bill and active penalty totals

This is particularly useful for reconciliation and auditing when reviewing a customer’s billing history.

### 4.3 Billing Adjustments
The application includes a bill adjustment interface and logic in the BillingController:

- BillAdjustment()
- SubmitAdjustment()
- GetBillByBillPeriod()

The model includes billing adjustment fields and options such as:

- usage adjustment
- current bill adjustment
- penalty adjustment
- present reading
- previous reading
- adjustment remarks

Although the current implementation still contains TODO placeholders for final persistence logic, the structure is already in place for a formal billing adjustment workflow.

---

## 5. Collections Update

### 5.1 Collections Search
The CollectionsController handles account search and collection summary retrieval:

- TMCWD/TMCWD.Application/Controllers/CollectionsController.cs

Main method:

- SearchAccounts(string q)

This endpoint:

1. searches for accounts by query string
2. loads customer information by customer ID
3. retrieves all billing records tied to the account
4. filters unpaid or non-waived bill records
5. calculates total bill amount and penalty amount
6. aggregates all outstanding balances into a collection summary

### 5.2 Collection Output Structure
The search result includes:

- account ID and account number
- customer name
- address
- total bill amount
- total penalty amount
- total due amount
- unpaid bill count
- individual bill records with:
  - billing reference ID
  - billing period
  - due date
  - bill amount
  - penalty
  - status

This gives the collections team an immediate view of which accounts need follow-up, how much is due, and which bills are still active.

---

## 6. Reading and Meter Management

### 6.1 Reading Transaction
The reading module is implemented in:

- TMCWD/TMCWD.Billing/ReadingTransaction.cs

It provides methods to:

- get a reading by ID
- get readings by account
- get readings by account and billing period
- get readings by reader and zone/book
- get current and previous readings
- save or update reading records
- save batch reading records
- update statuses by reading sheet
- retrieve readings by reading sheet ID

This makes the system capable of supporting meter reading workflows and bill generation based on actual consumption data.

### 6.2 Reading Sheets and Zone Books
The application supports reading sheet and zone book processes through:

- TMCWD/TMCWD.Billing/ReadingSheetTransaction.cs
- TMCWD/TMCWD.Billing/ZoneBookTransaction.cs

These classes handle:

- reading sheet retrieval by assigned user, zone, book, or billing date
- reading sheet status progression
- partial posting and completion of reading sheets
- retrieval of accounts linked to a reading sheet
- zone and book grouping for operational coverage

This is essential for meter readers and billing schedulers to work on grouped account schedules.

---

## 7. Water Rate and Tariff Logic

### 7.1 Water Rate Model
The tariff configuration is represented by the WaterRate model:

- TMCWD/TMCWD.Model/Billing/WaterRate.cs

It stores:

- account classification
- meter size
- minimum charge
- tier rates for consumption ranges
- effective date
- activity status
- creation and update timestamps

### 7.2 Water Rate Transaction
The tariff lookup logic is implemented in:

- TMCWD/TMCWD.Billing/WaterRateTransaction.cs

It supports:

- fetching a water rate by ID
- fetching all active or configured rates
- retrieving rate records by classification
- retrieving a rate by classification and meter size

This allows the system to compute billing based on the account’s classification and the meter size, which is a core requirement for utility billing systems.

### 7.3 Classification Enumeration
The classification system is defined in:

- TMCWD/TMCWD.Model/Enumerations.cs

The main account classifications include:

- Residential
- Government
- Industrial
- Commercial
- CommercialA
- CommercialB
- CommercialC
- Wholesale
- Bulk

---

## 8. Billing Data Model

The core billing data entity is defined under:

- TMCWD/TMCWD.Data/Entities/Billing.cs

Important fields include:

- AccountId
- JobOrderId
- BillingReferenceId
- PaymentTransactionId
- ReadingId
- BillingPeriod
- MaterialsAmount
- Penalties
- OtherCharges
- BillingAdjustment
- TotalBillAmount
- PaymentStatus
- CreatedBy / UpdatedBy
- DateCreated / DateUpdated
- RemainingAmount

This model provides the foundation for tracking customer billing obligations, transaction history, payment settlement, and remaining balances.

---

## 9. Status and Payment Workflow

The project uses enumerations to manage account, payment, and reading states in:

- TMCWD/TMCWD.Model/Enumerations.cs

Key statuses include:

- PaymentStatus: Unpaid, Paid, Overdue, Waived
- AccountStatus: Pending, Suspended, Active, Closed
- ReadingStatus: Created, InProgress, Completed, Deleted

This supports a full utility billing cycle:

1. account is active
2. reading is recorded
3. billing is generated
4. penalties may be added
5. payment is tracked
6. collection status is reviewed

---

## 10. Security and Session Configuration

The application startup includes:

- distributed memory cache
- session state management
- authentication and authorization middleware
- JWT bearer configuration

This is important because the application is a multi-role utility system where different user groups may access different pages or workflows, such as:

- meter readers
- customer representatives
- engineers
- administrators

The configured user roles include:

- SuperAdmin
- CustomerRepresentative
- Engineer
- MeterReader
- Guest

---

## 11. User Experience and Functional Summary

The updated billing and collection web application now supports the following business flow:

### Billing Workflow
1. Account is identified.
2. Meter reading is captured or retrieved.
3. Water rate is selected based on classification and meter size.
4. Consumption is calculated using current and previous readings.
5. Fees, penalties, and other charges are added.
6. Total bill amount is generated.
7. Billing history is stored and reviewed.

### Collection Workflow
1. Collection staff searches customer accounts.
2. The system summarizes unpaid bills and penalties.
3. Outstanding balances are calculated per account.
4. Individual bills are displayed with due date and status.
5. Collection follow-up can be prioritized based on total due amount.

---

## 12. Current Status and Notes

The current codebase reflects a functional foundation for the billing and collection system. It already includes the structure for:

- account-based billing review
- collections account lookup
- reading-based usage tracking
- penalty tracking
- tariff lookup by classification and meter size
- reading sheet management

Some areas still contain placeholder or TODO logic, especially in billing adjustment processing, but the overall architecture and workflow are already in place for expansion and refinement.

---

## 13. Recommended Next Improvements

To complete and strengthen the system, the following can be prioritized:

1. finalize the persistence and validation logic for billing adjustments
2. standardize due-date calculation rules across all billing scenarios
3. improve the penalty and waiver workflow for account-specific collections
4. add full reporting for aging receivables and unpaid balances
5. implement dashboard summaries by zone, book, and billing period
6. add audit trails for billing corrections and collections activity

---

## 14. Conclusion

The updated billing and collection web application reflects a practical utility billing system design for water district operations. It combines account management, meter reading records, billing computations, tariff logic, and collections oversight into a single web-based workflow. The architecture is modular and extensible, which makes it well suited for further enhancement, reporting, and operational automation.

This update lays the foundation for a reliable utility billing process that can be expanded into a full production-ready billing and collections platform.
