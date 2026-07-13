# Pureledger Automatic Backups Design

## Goal

Add a small, dependable backup workflow to Pureledger that follows the style of the FreightForwarding V3 deployment guide: PostgreSQL backups are created automatically on the VPS and pulled automatically to the developer's Windows laptop.

## Scope

The workflow backs up the PostgreSQL database `pureledgerdb` from the Pureledger VPS at `72.60.209.26`. It retains backups for 14 days on the VPS and 30 days on the laptop. The local destination is `C:\Users\Claude\Desktop\pureledger_backups`.

The VPS job runs daily at 2:00 AM in the VPS timezone. The Windows pull job runs daily at 3:30 AM and runs as soon as possible after a missed start when the laptop was off.

Cloud storage, dashboards, notifications, application-file backups, and additional encryption are outside this change.

## Architecture

The VPS uses a short Bash script invoked by a systemd oneshot service and persistent timer. The script creates a PostgreSQL custom-format dump with `pg_dump -F c`, verifies that PostgreSQL can read its catalog with `pg_restore --list`, creates a SHA-256 checksum, atomically publishes both files into `/var/backups/pureledger`, and deletes completed backup pairs older than 14 days.

A non-sudo Linux account named `pureledger-backup` receives read-only group access to `/var/backups/pureledger`. A dedicated Ed25519 SSH key generated on the laptop authenticates that account without storing the VPS root password. The account is used only by the Windows backup task.

The laptop uses a PowerShell script invoked by Windows Task Scheduler. It obtains the server file list over SSH, uses `scp` to download only backup pairs that are not already local, verifies every downloaded dump against its checksum, atomically renames verified temporary downloads, and deletes complete local backup pairs older than 30 days.

## Repository Files

- `deployment/backup/pureledger-backup.sh`: create, verify, checksum, publish, and expire VPS backups.
- `deployment/backup/pureledger-backup.service`: systemd oneshot service definition.
- `deployment/backup/pureledger-backup.timer`: daily 2:00 AM schedule with persistent missed-run behavior.
- `deployment/backup/install-vps-backup.sh`: small idempotent installer for the VPS files, permissions, database credential file, backup user, service, and timer.
- `deployment/backup/pull-pureledger-backups.ps1`: download, verify, log, and expire laptop backups.
- `deployment/backup/install-windows-task.ps1`: register or replace the daily 3:30 AM scheduled task.
- `VPS_DEPLOYMENT_GUIDE.md`: ordered setup, verification, monitoring, manual pull, restore, and troubleshooting commands.

## VPS Backup Behavior

The Bash script uses strict error handling and an exclusive lock so two backup runs cannot overlap. Configuration uses environment variables with Pureledger defaults for the database name, user, host, port, backup directory, and retention period. PostgreSQL authentication is provided by `/root/.pgpass`, mode `0600`; passwords are not embedded in repository files, process arguments, or the guide.

Each run writes timestamped temporary files. A dump is accepted only if `pg_dump` succeeds, the file is non-empty, and `pg_restore --list` succeeds. The script then writes a checksum compatible with both Linux `sha256sum` and PowerShell verification. Only after validation are the dump and checksum renamed to their final names.

Retention removes `.dump` and matching `.sha256` files older than 14 days. Temporary files from interrupted runs are cleaned up on the next run. Script messages go to the systemd journal.

## Laptop Pull Behavior

The PowerShell script uses the dedicated private key and connects to `pureledger-backup@72.60.209.26`. It creates the destination and `logs` directory when absent. Server filenames must match the timestamped Pureledger backup naming convention before they are used in local paths.

For each remote dump not already present with its checksum, the script downloads both files with `.partial` suffixes. It calculates SHA-256 locally and compares it case-insensitively with the checksum file. A matching pair is renamed to its final names. A mismatch fails the run and leaves existing valid backups untouched; invalid partial files are removed.

The script deletes local dump/checksum pairs older than 30 days only after transfer processing. It appends a dated log describing downloads, validation, retention, and errors. A nonzero exit code makes the Windows task show failure.

## Installation and SSH Setup

The deployment guide uses the existing root SSH login once to install the VPS components and create the non-sudo backup account. On the laptop, the user generates a dedicated Ed25519 key under their Windows SSH directory. The public key is copied to the VPS account with explicit ownership and `0600` permissions. The guide includes a passwordless SSH test before registering the scheduled task.

The Windows task runs as the current Windows user without storing that user's password, starts at 3:30 AM daily, starts after a missed schedule, and does not start a second instance while one is running. The user must be logged on for the task to execute. Its action calls `powershell.exe` with the repository pull script and explicit server, identity-file, and destination parameters.

## Failure Handling

- A PostgreSQL dump or validation failure produces no final backup filename.
- An overlapping VPS run exits without starting another dump.
- An unavailable VPS or laptop network connection fails that pull run without modifying valid local backups.
- A partial transfer never appears as a completed backup.
- A checksum mismatch prevents publication of the local dump.
- Retention operates only on files matching the Pureledger backup naming convention.
- The systemd journal and local PowerShell log provide commands and evidence for troubleshooting.

## Verification

Repository verification checks Bash syntax, PowerShell parsing, and systemd unit structure where the relevant tools are available. Deployment verification then runs the service manually, confirms a non-empty dump/checksum pair, checks `pg_restore --list`, tests passwordless SSH and SCP, invokes the PowerShell pull script manually, compares the local checksum, starts the Windows scheduled task on demand, and checks both VPS and laptop logs.

## Documentation Outcome

`VPS_DEPLOYMENT_GUIDE.md` will replace its minimal cron-only backup instructions with a copy-paste-ready sequence modeled on the FreightForwarding V3 guide. It will clearly label which commands run in local PowerShell and which run after SSHing into the VPS, use placeholders only for secrets that the operator must provide, and include manual backup, automated backup, download, local restore, VPS restore, status, log, and failure-recovery commands.
