# Kratos User Migration Guide

This document describes how to migrate existing Homassy users to the Ory Kratos identity system.

## Overview

The migration process:
1. Reads existing users from the Homassy database
2. Creates corresponding Kratos identities with user traits
3. Links the Kratos identity ID back to the user record
4. Marks email addresses as verified (since users were already verified)

## Prerequisites

Before running the migration:

1. **Database Backup**: Create a full backup of the production database
   ```bash
   docker exec homassy-postgres pg_dump -U homassy homassy > backup_$(date +%Y%m%d_%H%M%S).sql
   ```

2. **Services Running**: Ensure PostgreSQL and Kratos are running and healthy:
   ```bash
   docker-compose up -d postgres homassy.kratos
   curl http://localhost:4434/health/alive  # Should return {"status":"ok"}
   ```

3. **EF Migrations Applied**: The `AddKratosIdentityId` migration must be applied:
   ```bash
   dotnet run --project Homassy.Migrator -- migrate
   ```

## Migration Commands

### Check Migration Statistics

See how many users need to be migrated:

```bash
dotnet run --project Homassy.Migrator -- kratos-stats
```

Output:
```
Total Users:       150
Migrated Users:    0
Pending Migration: 150
Progress:          0.0%
```

### Dry Run (Recommended First)

Simulate the migration without making any changes:

```bash
dotnet run --project Homassy.Migrator -- migrate-to-kratos --dry-run
```

This will:
- Connect to the database and Kratos
- List all users that would be migrated
- Check for potential conflicts (emails already in Kratos)
- NOT create any Kratos identities or update user records

### Run Migration

Execute the actual migration:

```bash
dotnet run --project Homassy.Migrator -- migrate-to-kratos
```

Or use the shell script:

```bash
./Homassy.Migrator/Scripts/migrate-users-to-kratos.sh
```

### Verify Migration

After migration, verify all users have been migrated:

```bash
dotnet run --project Homassy.Migrator -- verify-kratos
```

## Using the Shell Script

The shell script provides a user-friendly wrapper:

```bash
# Show help
./Homassy.Migrator/Scripts/migrate-users-to-kratos.sh --help

# Check statistics
./Homassy.Migrator/Scripts/migrate-users-to-kratos.sh --stats

# Dry run
./Homassy.Migrator/Scripts/migrate-users-to-kratos.sh --dry-run

# Full migration (with confirmation prompt)
./Homassy.Migrator/Scripts/migrate-users-to-kratos.sh

# Verify migration
./Homassy.Migrator/Scripts/migrate-users-to-kratos.sh --verify
```

## Data Mapping

The following data is migrated from Homassy to Kratos traits:

| Homassy Field | Kratos Trait | Notes |
|--------------|--------------|-------|
| User.Email | traits.email | Used as identifier |
| User.Name | traits.name | Full name |
| User.FamilyId | traits.family_id | Optional |
| UserProfile.DisplayName | traits.display_name | |
| UserProfile.ProfilePictureBase64 | traits.profile_picture_base64 | |
| UserProfile.DateOfBirth | traits.date_of_birth | Format: YYYY-MM-DD |
| UserProfile.Gender | traits.gender | |
| UserProfile.DefaultCurrency | traits.default_currency | Enum → String |
| UserProfile.DefaultTimeZone | traits.default_timezone | Enum → String |
| UserProfile.DefaultLanguage | traits.default_language | hu/de/en |

### Enum Mappings

**Currency:**
- Currency.Huf → "HUF"
- Currency.Eur → "EUR"
- Currency.Usd → "USD"
- etc.

**Timezone:**
- UserTimeZone.CentralEuropeStandardTime → "CentralEuropeStandardTime"
- UserTimeZone.GreenwichStandardTime → "GMTStandardTime"
- etc.

**Language:**
- Language.Hungarian → "hu"
- Language.German → "de"
- Language.English → "en"

## Handling Conflicts

### Email Already Exists in Kratos

If a user's email already exists in Kratos (e.g., from a previous partial migration):

1. The migration will detect the existing identity
2. It will link the Homassy user to the existing Kratos identity
3. The user will be marked as "Already exists" in the output

### Migration Failures

If a user fails to migrate:

1. The migration continues with remaining users
2. Failed users are logged with error details
3. Re-run the migration to retry failed users
4. Exit code 2 indicates partial success

## Rollback Plan

If the migration needs to be rolled back:

### Option 1: Database Restore

Restore the backup taken before migration:

```bash
docker exec -i homassy-postgres psql -U homassy homassy < backup_YYYYMMDD_HHMMSS.sql
```

### Option 2: Clear Kratos Identity IDs

Remove the KratosIdentityId links (keeps Kratos identities):

```sql
UPDATE "Users" SET "KratosIdentityId" = NULL;
```

### Option 3: Delete Kratos Identities

Delete all Kratos identities (use with caution):

```bash
# List all identities
curl http://localhost:4434/admin/identities

# Delete specific identity
curl -X DELETE http://localhost:4434/admin/identities/{identity_id}
```

## Post-Migration

After successful migration:

1. **Verify Users Can Login**
   - Test login with a few users using Passkey or Magic Link
   - Ensure session cookies are set correctly

2. **Update Frontend**
   - Deploy the updated frontend with Kratos integration
   - Users will need to set up Passkeys on first login

3. **Monitor Logs**
   - Check Kratos logs for any authentication issues
   - Monitor API logs for session validation errors

4. **Cleanup**
   - Consider removing the old UserAuthentication data
   - Update documentation

## Troubleshooting

### Cannot Connect to Kratos

```
ERROR: Cannot connect to Kratos Admin API
```

1. Check if Kratos is running: `docker-compose ps`
2. Check Kratos logs: `docker logs homassy-kratos`
3. Verify the Admin URL in appsettings.json

### Identity Creation Failed

```
ERROR creating identity: {"error":{"message":"..."}}
```

1. Check the error message for details
2. Common issues:
   - Invalid email format
   - Traits don't match schema
   - Database constraint violations

### Migration Hangs

If the migration appears stuck:

1. Check database connectivity
2. Check Kratos logs for errors
3. Restart the migration (it will skip already-migrated users)

## Configuration

Environment variables for the migration:

```bash
# Connection string (can also be in appsettings.json)
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=homassy;Username=homassy;Password=..."

# Kratos Admin URL
Kratos__AdminUrl="http://localhost:4434"
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Fatal error (configuration, database, etc.) |
| 2 | Partial success (some users failed) |
| 3 | Verification failed (users not migrated) |
