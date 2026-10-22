# HGRAS User Manual

## Contents

1. [Purpose and navigation](#1-purpose-and-navigation)
2. [Accounts](#2-accounts)
3. [Hospital groups](#3-hospital-groups)
4. [Facilities and departments](#4-facilities-and-departments)
5. [Membership and roles](#5-membership-and-roles)
6. [Dashboard](#6-dashboard)
7. [Inventory](#7-inventory)
8. [Resource search](#8-resource-search)
9. [Transfer workflow](#9-transfer-workflow)
10. [Notifications and audit](#10-notifications-and-audit)
11. [Operational registers](#11-operational-registers)
12. [Reporting](#12-reporting)
13. [Organisation requests](#13-organisation-requests)
14. [Account administration](#14-account-administration)
15. [Troubleshooting](#15-troubleshooting)

## 1. Purpose and navigation

HGRAS coordinates resources between hospitals in the same group. It records where stock is held, how much can safely be shared, who authorises a movement and what the receiving hospital accepts.

The hospital-network workspace is available at `/Network` in both applications. Its tabs are Overview, Facilities, Inventory, Transfers, Equipment, Staff, Capacity, Procurement, Notifications, Audit and Access. Access is available to group administrators.

The staff portal also contains platform account administration and a separate organisation-request workflow. For installation and startup, see the [README](../README.md).

## 2. Accounts

### First administrator

On a fresh database, open the staff portal's `/Account/Register` page. Enter your own email, password and password confirmation. The first successful registration receives `ADMIN` and signs in.

Once an administrator exists, anonymous staff registration closes. Subsequent staff accounts are created by an administrator. No shared default credentials are supplied.

### Create a staff account

1. Sign in as a platform administrator.
2. Open **System users** and choose **Create**.
3. Enter the new account's email and password.
4. Review the account editor. Later accounts do not receive administrative privileges automatically.
5. Assign platform roles only when the account needs the corresponding administration or organisation-workflow permissions.
6. Assign hospital-network membership separately through **Access**.

Platform administrators manage account identity. Group administrators manage access inside their hospital group. A group administrator does not automatically receive platform account-management rights.

### Register an organisation

Use **Register organisation** in the organisation portal. Supply the requested organisation, registration and contact details, account email and password. Upload a logo where required or offered.

This creates an organisation account. It does not create a hospital group, add facility inventory or grant access to other hospitals. A group administrator may assign an existing registered account network membership.

### Sign in and recover access

Use the sign-in page for the portal you need. Staff sign-in has a Show/Hide password control. Use **Remember me** only on a trusted computer and sign out after using a shared device.

For a forgotten password, use **Forgot password**. Recovery requires configured SMTP delivery and an eligible account. If delivery is unavailable, contact the administrator; there is no fallback password.

## 3. Hospital groups

1. Open the network workspace as a platform administrator.
2. Expand **Create a hospital group**.
3. Enter the real group name and submit.
4. The creating account becomes that group's first `GROUP_ADMIN`.

Use **Switch group** when you belong to multiple groups. Membership in one group does not grant access to another. A platform `ADMIN` role alone does not grant unrestricted access to other groups' network data.

## 4. Facilities and departments

### Add a hospital

Group administrators open **Facilities** and enter the hospital name, location and responsible officer. Use consistent real names so staff can identify the correct supplying and receiving teams.

### Add a department

Select the hospital, enter the department name and responsible officer, then submit. Group administrators work across the group; hospital administrators are restricted to their assigned hospital.

## 5. Membership and roles

A group administrator opens **Access**, enters an existing account's email, selects its network role and chooses its hospital. `GROUP_ADMIN` applies to the whole group; other roles require a hospital assignment.

| Role | Responsibilities |
| --- | --- |
| `GROUP_ADMIN` | Group setup, facilities, membership and resource coordination across the group. |
| `HOSPITAL_ADMIN` | Departments, inventory, transfers and operational records at the assigned hospital. |
| `DEPARTMENT_MANAGER` | Transfer requests for the assigned hospital. |
| `STORES` | Inventory entry/reconciliation and transfer actions at the assigned hospital. |
| `CLINICAL` | Transfer requests and recorded bed/service capacity at the assigned hospital. |
| `EQUIPMENT` | Equipment records at the assigned hospital. |
| `HR` | Aggregate staffing records at the assigned hospital. |
| `PROCUREMENT` | Procurement/replenishment records at the assigned hospital. |
| `AUDITOR` | Read permitted records without making inventory or transfer mutations. |

Group members can search inventory across their authorised group to identify suppliers. Operational registers and transfer histories are restricted by hospital assignment. Group administrators see the group-wide view.

An administrator cannot change their own group membership in this form. Another group administrator must make that change.

## 6. Dashboard

The overview displays connected facilities, transfers in progress, pending approvals, low-stock alerts, transfer-status and inventory-health charts, priority-sorted activity and expiry warnings.

Counts come from saved records visible to your membership. Empty datasets display an empty chart state rather than invented activity.

Use **Refresh data** to retrieve current saved records. Times are shown in UTC. Inventory over 24 hours old is flagged for verification; records are not automatic sensor updates.

## 7. Inventory

Under **Inventory**, expand **Record a stock lot**.

| Field | Meaning |
| --- | --- |
| Hospital | Facility holding the stock. |
| Resource name | Consistent description of the item. |
| Category | Supply, Medicine, Equipment or Consumable. |
| Unit | Counting unit: cylinders, boxes, individual items, etc. |
| Quantity | Verified on-hand count. |
| Minimum safe reserve | Quantity the supplying hospital should retain. |
| Reorder level | Threshold used for low-stock warnings. |
| Batch | Lot identifier; required for medicines. |
| Expiry date | Future expiry date; required for medicines. |
| Storage / handling | Handling requirements; required for medicines. |

Medicines require batch, expiry and handling information. Newly entered expired stock is rejected. Each lot has a separate identity and last-recorded timestamp.

### Shareable quantity

For eligible, unexpired stock:

```text
Shareable quantity = On hand - Reserved - Minimum safe reserve
```

Negative results and expired lots show zero shareable units. Compare quantities only when the resource and unit match the requirement.

### Reconciliation and replenishment

1. Expand **Reconcile or replenish a stock lot**.
2. Select the lot.
3. Enter its current displayed count and the new verified count.
4. Enter the reserve and reorder thresholds.
5. Supply a reason or replenishment reference and save.

Stale counts and adjustments that would invalidate an existing reservation are rejected. Refresh and resolve the reservation before retrying. Every successful adjustment is audited.

## 8. Resource search

Enter a resource search and quantity under **Find safely shareable resources**. Review eligible lots, hospitals, batches, expiry dates, handling information and timestamps.

Suggestions use recorded availability, reserves and expiry. They do not calculate transport distance or verify clinical compatibility. Authorised officers must confirm suitability and transport before approving a transfer.

## 9. Transfer workflow

### Request

The receiving hospital opens **Transfers** and expands **Request an inter-hospital transfer**. Select the source stock lot, receiving hospital, quantity and priority: Routine, Urgent or Critical. Describe the need, suitability requirements and delivery arrangements.

Submitting a request does not reserve stock. Approval rechecks what can actually be supplied.

### Approve or reject

The supplying hospital's stores officer, hospital administrator or group administrator reviews the request. For approval, confirm the suitability/storage/delivery review, enter a note and record `APPROVED`.

Approval reserves the quantity atomically. It fails if another approval has already consumed the safe availability. Rejection requires a note and does not allocate stock.

An approved transfer can also be rejected before dispatch. This releases its reservation.

### Dispatch

The supplying hospital records `DISPATCHED` and enters transport details: vehicle/reference, responsible team and handling information as appropriate.

Dispatch subtracts the source quantity and clears its reservation. The receiving inventory is not credited before delivery.

### Receive and reconcile

The receiving hospital inspects the delivery and records `RECEIVED` with the number of usable units accepted. Explain missing, damaged or unusable items in the receipt note.

Only accepted units enter destination inventory. A separate destination lot preserves the source resource, unit, batch, expiry and handling metadata. A duplicate receipt cannot credit stock again.

### Review the result

Check both inventories, transfer status, accepted/dispatched quantities, notifications and audit history. Both hospitals receive an in-app notice for each transfer stage.

### Worked example

A hospital holds 15 oxygen cylinders and retains a minimum reserve of 5. It can initially share 10. Another hospital requests 5:

| Stage | Source on hand | Source reserved | Destination addition |
| --- | ---: | ---: | ---: |
| Requested | 15 | 0 | 0 |
| Approved | 15 | 5 | 0 |
| Dispatched | 10 | 0 | 0 |
| Received in full | 10 | 0 | 5 |

If only 4 usable cylinders arrive, receipt adds 4 and records the discrepancy. These figures illustrate the process; they are not seeded application data.

## 10. Notifications and audit

**Notifications** lists unread transfer updates within your authorised scope. **Mark as read** acknowledges a notice for your account. An in-app notice does not imply an email or SMS was sent.

**Audit** shows the latest 100 permitted events with actor, action, transfer reference, note and UTC timestamp. Events cover group/facility setup, membership, inventory, transfers and operational updates.

## 11. Operational registers

These modules store staff-reported coordination information. They do not automatically move stock or authorise clinical actions.

| Module | What to record | Responsible role |
| --- | --- | --- |
| Equipment | Equipment pools, location/condition notes, status, counts and maintenance due dates. | Equipment officer or hospital/group administrator. |
| Staff | Aggregate qualified role pools, availability, assignment notes and review dates. | HR coordinator or hospital/group administrator. |
| Capacity | Staffed, usable bed/service capacity, available/total counts and verification notes. | Clinical officer or hospital/group administrator. |
| Procurement | Replenishment needs, total/outstanding units, purchasing status, due dates and responsible officers. | Procurement officer or hospital/group administrator. |

Staff credential verification and actual roster approval remain with the responsible coordinator. Capacity records are not patient-transfer authorisations. Procurement records do not submit orders, process payments or add inventory; record verified deliveries through inventory entry/reconciliation.

### Add or update a record

Leave **Existing record ID** blank for a new record. To update one, enter its displayed ID and complete revised fields. Select the hospital, enter status/counts/details and save. Available or outstanding counts must be nonnegative and cannot exceed the total.

## 12. Reporting

Use **Export stock CSV** under Inventory. It includes resource, hospital, unit, on-hand quantity, reservations, minimum reserve, shareable quantity and last-recorded time for your authorised group.

Review dashboard charts alongside the inventory report. Financial savings and predictive forecasts are not calculated.

## 13. Organisation requests

The staff portal retains **Organisation requests**, **Resource catalogue** and organisation administration. This workflow uses the original catalogue, not hospital-network stock lots.

An organisation submits its request through the organisation portal. Permitted staff stages are:

```text
INITIAL → APPROVED → RELEASED → DISPATCHED → COMPLETED
    └──→ REJECTED
```

`APPROVER` reviews initial requests; `RELEASER` releases approved requests; `DISPATCHER` dispatches/completes them. A platform `ADMIN` can perform permitted transitions. Approval deducts original-catalogue stock once and records history.

Organisation users can edit/delete only their own initial requests. Group membership roles do not grant these platform workflow roles.

Do not combine this catalogue with hospital-network inventory or treat them as one stock ledger. Existing organisation records are preserved but are not automatically imported as network facilities.

## 14. Account administration

Platform administrators can edit supported account details/roles and enable or disable accounts. Password hashes and security tokens are not editable through these screens.

The forms prevent disabling your own account or removing the last platform administrator. Group/hospital membership remains a separate **Access** responsibility.

## 15. Troubleshooting

| Situation | Action |
| --- | --- |
| No group listed | Check group membership. An organisation registration is not a hospital group. |
| Action forbidden | Ask the group administrator to check your role and hospital assignment. |
| Insufficient shareable stock | Refresh inventory, reduce the quantity or select another eligible lot. |
| Transfer changed during review | Reopen it before submitting an action. |
| Count adjustment rejected | Check the expected count and outstanding reservations. |
| Form token expired | Reopen the form; do not resubmit an old browser tab. |
| Workspace temporarily unavailable | Have the administrator check SQL Server, connection settings and migration permissions. |
| Recovery email unavailable | Have the administrator check SMTP configuration. |

Installation-specific guidance is in the [README](../README.md#troubleshooting). Back up operational database files and private configuration through the deployment administrator's normal process; this repository is application source, not an operational-data backup.
