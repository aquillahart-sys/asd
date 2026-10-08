# Chapters 16–17 setup and verification

The application targets the existing `enrollment_db` schema. The SQL file
`Data/chapter16-17-schema.sql` creates missing tables and adds the authentication
and payment columns needed by the application. Review it against your database
first; do not apply it to a database that has not been backed up.

## Database and first administrator

1. Back up the database before any schema change. For MariaDB, for example:

   ```powershell
   mariadb-dump --single-transaction enrollment_db > enrollment_db-before-ch16-17.sql
   ```

2. Apply `Data/chapter16-17-schema.sql` using a MariaDB client configured for
   your database. The script uses `enrollment_db`; adjust that name consistently
   if your local database has a different name.
3. Configure `ConnectionStrings__DefaultConnection` for the application instead
   of committing database credentials to `appsettings.json`.
4. If the database has no users, create its initial administrator from the
   project directory:

   ```powershell
   dotnet run --project EnrollmentSystem_G4 -- --bootstrap-admin
   ```

   The command prompts for the account details and password; it does not echo
   the password. Create Registrar and Cashier accounts through the administrator
   user-management page.
5. Existing unsalted password hashes are intentionally not accepted. Reset each
   account that must be retained through the local administrative command:

   ```powershell
   dotnet run --project EnrollmentSystem_G4 -- --reset-password <username>
   ```

   Password changes made this way are written to the audit log.

## Chapter 17 workflow

1. Sign in as an Administrator or Registrar and create active subject offerings
   for the configured academic year and semester.
2. Create a pending enrollment for a student and select distinct offerings.
3. Assess the enrollment. Tuition is calculated from total units and the
   configured `Enrollment:RatePerUnit`; fees and downpayment rate are configured
   in `appsettings.json`.
4. As an Administrator or Cashier, record one or more downpayments up to the
   required total. A Registrar or Administrator can then confirm the enrollment
   after the required downpayment is met. Confirmation creates monthly schedules
   for the remaining balance.
5. Record monthly payments against their own installment schedules. Review
   receipt history, balances, and the Certificate of Registration (COR); the COR
   is available only after confirmation.

## Administrator user management and audit history

- The Administrator-only **User Accounts** page can create accounts, edit
  usernames, names, email addresses, and roles, and disable accounts. Password
  values and hashes are not shown or editable there; use the documented local
  password-reset command when an account needs a reset.
- Administrators cannot change their own role or disable their own account.
  The last active Administrator cannot be demoted or disabled.
- The Administrator-only **Audit History** page includes successful and failed
  login attempts, logout, account changes, program changes, enrollment and
  payment actions. Account edits are recorded with the changed fields; password
  values, hashes, and salts are never written to audit details.

## Negative and backup checks

Use a disposable database copy for these checks:

- Reject blank/unknown credentials with the same login error; reject inactive
  accounts and verify role-restricted pages/actions return access denied.
- Reject assessment without offerings, duplicate term enrollment, invalid or
  inactive offerings, duplicate offerings, and confirmation below the required
  downpayment.
- Reject non-positive payments, payments over the remaining downpayment or
  installment, reused receipt numbers, invalid payment types, and schedules
  belonging to another enrollment.
- Confirm balances equal assessment minus recorded payments, schedule totals
  equal the post-downpayment balance, and the COR remains blocked until
  confirmation.
- Restore the pre-migration backup to a separate database and verify the restored
  schema and records before considering the backup ready.

## Verified test results

The following checks were executed over HTTPS against a disposable XAMPP MariaDB
schema with fictional student data and temporary Administrator, Registrar, and
Cashier accounts. The test and restore databases were removed after verification.
These are HTTP integration checks, not a manual graphical-browser demonstration.

| Test | Expected result | Actual result |
| --- | --- | --- |
| Login with each of the three roles | Successful login and role-appropriate redirect | Pass |
| Invalid password and inactive account | Same generic login error; no authenticated access | Pass |
| Student list and role-restricted pages | Cashier can view students; cashier enrollment/user/audit access and Registrar user/payment access are denied | Pass |
| Create and retrieve a student as Registrar | New fictional record appears in student list; duplicate registration is rejected | Pass |
| Pending enrollment with duplicate offering IDs | Rejected without creating an enrollment; validation message is visible | Pass |
| Three-unit assessment at configured rate | Assessment ₱1,500.00; required downpayment ₱300.00 | Pass |
| Zero payment and downpayment above the required amount | Rejected with validation feedback; no invalid payment saved | Pass |
| ₱299 downpayment followed by confirmation attempt | Enrollment remains assessed; no schedules are created | Pass |
| Remaining ₱1 downpayment and Registrar confirmation | Enrollment confirms; four schedules cover the remaining ₱1,200.00 | Pass |
| COR before and after confirmation | Denied before confirmation; after confirmation shows the fictional student and selected subject | Pass |
| Monthly installment above its remaining amount | Rejected | Pass |
| Valid ₱300 monthly payment | Receipt saved; installment is marked paid | Pass |
| Duplicate receipt number | Rejected; existing receipt remains unchanged | Pass |
| History, statement of account, and dashboard | Include successful receipts; assessment ₱1,500.00 less payments ₱600.00 gives ₱900.00 remaining | Pass |
| Audit review | Login success/failure, logout, enrollment, assessment, confirmation, payments, and account disable events are present | Pass |
| Administrator manager and edit history | Created and edited a staff account, changed its role, disabled it, edited a program, created a subject, and verified those events in the audit view; login/logout/failure events were present and test passwords were absent | Pass |
| Role enforcement after account editing | Cashier received HTTP 403 for user-management and audit pages | Pass |
| Logout and account disable | Protected access is redirected to login; disabled account cannot sign in and its existing cookie is revoked | Pass |
| SQL backup and restore | Non-empty export restored into a separate schema; users, students, enrollment, payments, and audit rows were present | Pass |

**Defect corrected during testing:** duplicate selected offerings were rejected by
the server, but the form hid the property-level validation error. The enrollment
form now displays all model errors; the duplicate-selection test was rerun and
the rejection message was visible.

## Demonstration sequence

1. Sign in as Administrator or Registrar and show the fictional student,
   program, subject, and active offering.
2. Create the pending enrollment, review its assessment and downpayment, then
   record the downpayment as Cashier.
3. Confirm the enrollment as Administrator or Registrar and show the COR and
   generated schedule.
4. Record a monthly payment as Cashier; show its receipt in payment history,
   the updated balance/dashboard, and the audit entry.
5. Demonstrate one controlled failure, such as a duplicate receipt or a
   Cashier opening user management, then sign out and show that a protected page
   requires login.

Use fresh fictional demonstration records and do not reuse any real account
passwords.
