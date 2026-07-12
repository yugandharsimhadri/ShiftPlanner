# Deploying to the team (shared network path)

The desktop app has no server — it's a self-contained WPF exe with a SQLite
database file (`shiftplanner.db`) sitting next to it. The whole app runs
directly off a shared network folder so everyone reads/writes the same
database.

## Prerequisites (one-time)

- A real SMB network share or mapped drive (e.g. `\\server\Shared\ShiftPlanner`).
  **Do not** use a OneDrive/Dropbox/Google Drive synced folder — cloud sync
  does not respect file locks and will corrupt `shiftplanner.db`.
- Everyone who needs to run the app has Modify permission on that folder.

## Publish a new build

Run from the `desktop` folder:

```
dotnet publish -c Release -r win-x64 --self-contained true
```

Output goes to `bin\Release\net10.0-windows\win-x64\publish\`. This build
needs no .NET runtime on teammates' machines.

## Push it to the share

1. Copy the **entire contents** of the `publish\` folder to the shared
   folder, overwriting the previous version.
   - Do **not** overwrite or delete `shiftplanner.db` — that's the live data.
2. Each teammate runs `ShiftPlanner.Desktop.exe` from that shared path
   (a desktop shortcut to the UNC or mapped-drive path works well).
3. First launch may trigger a Windows SmartScreen warning ("unrecognized
   publisher") since the exe isn't code-signed — click **More info → Run
   anyway**. If this is a recurring annoyance for the team, add the share
   to trusted locations via Group Policy.

## Known limitations of this setup

- **No live sync between running instances.** Each open app loads its data
  once at startup. If a teammate saves a change, others won't see it until
  they restart the app.
- **No conflict detection.** If two people edit the same record and save
  around the same time, the second save silently wins — there's no merge or
  warning. In practice this is fine for a small team that isn't editing the
  same records simultaneously.
- **Lock contention** (two people saving at the exact same instant) shows a
  friendly "Someone else is saving changes right now" message instead of
  crashing (see `App.xaml.cs`), then the user just retries.

## Backups

`shiftplanner.db` on the share is the single copy of all data. Periodically
copy it somewhere else (e.g. a dated backup folder) — there's no built-in
backup/versioning.
