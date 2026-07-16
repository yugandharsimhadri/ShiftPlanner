# ShiftPlanner — User Guide

This guide covers day-to-day use of ShiftPlanner on both the **Web app**
and the **Mobile app**. For how the system is built, see the
[Technical Design Document](TECHNICAL_DESIGN.md); for step-by-step flow
diagrams, see [Workflows](WORKFLOWS.md).

## 1. What ShiftPlanner is

ShiftPlanner helps a team plan and track who's working which shift, on
which day. One team's data is completely separate from every other team's —
you'll only ever see the teams you've been added to. It's a self-hosted
tool: your organization runs its own server, and you point the Web or
Mobile app at that server's address.

## 2. Signing in

**Web**: open the app's URL, sign in with your email and password (or ask
your admin whether your organization uses phone-number login instead).

**Mobile**: open the app, enter your **server address** first (your admin
will give you this — it looks like `http://yourserver:5080`), then sign in
the same way. You can change the server address later from the **Profile**
tab.

New here? Use "Create an account" — but note that just having an account
doesn't put you on a team. Either an admin adds you to their team (by your
email or phone number — you don't need to have signed up first), or you
create your own team the first time you sign in with no teams yet.

## 3. Picking a team

If you belong to more than one team, you'll see a team picker right after
signing in. Pick one to work in — everything you see afterward (roster,
team members, settings) belongs to that team only. You can switch teams
any time from **Profile → Switch team** (Mobile) or the team switcher in the
sidebar (Web).

## 4. Roster

This is the main screen — a calendar of who's working what.

- **Web**: a full month grid. Each row is a team member, each column a day.
  Click a cell to assign, change, or clear a shift.
- **Mobile**: one day at a time. Swipe with the arrows or tap "Today" to
  jump around; tap a team member's row to assign their shift for that day.
  Use **"Showing: All — tap for Just me"** to filter down to just your own
  shifts.

Weekends (or whatever days your team has set as default off-days) are
visually distinguished. If your team has comp-offs turned on, working one
of those days automatically earns you a comp-off — you'll see it reflected
in Reports.

**Who can edit:** Viewers can see the roster but not change it. Editors and
Admins can tap/click any cell to assign a shift.

### Copy Forward

Instead of re-building next month's roster from scratch, use **Copy fwd**
to copy an existing month onto a new one — either matching the same weekday
pattern or the exact same dates. Inactive team members are skipped
automatically, and anything that would conflict with an existing entry,
holiday, or leave is flagged for you to look at rather than silently
overwritten.

### Export

Use **Export** to download the current month as an Excel (`.xlsx`) or CSV
file — handy for printing or sharing outside the app.

## 5. Team Members

Your team's directory — everyone who's on the roster, plus their access
level. (This used to be two separate screens — "Employees" and "Team" — now
merged into one, since it's almost always the same list of people either
way.)

Each row shows:
- **Name and code** (e.g. `EMP-004`) — the code is your organization's own
  reference number, editable by an Admin.
- **Track**, **job role**, and **location**, if set.
- **Access role** — `Viewer`, `Editor`, or `Admin` (see §9 below).
- Whether they **have a login yet** — someone can be on the roster and not
  have signed in yet; the moment they do sign in with a matching email or
  phone, their access activates automatically.

**Admins** can:
- **Add** a team member (name, phone, code, track/subtrack, job role,
  location, employment type, join date, access role).
- **Edit** any of the above, or mark someone Inactive rather than deleting
  them outright (their roster history stays intact).
- **Change role** or **Remove** someone from the team.
- Set **Team Lead** / **Co-Lead** — a label, not an extra permission tier;
  exactly one person can be Team Lead, at most one Co-Lead.

## 6. Live Availability *(Web only, for now)*

Separate from the planned roster — a simple "I'm free right now" toggle,
for spotting who's actually around at this moment without waiting for the
next scheduled shift.

- Go to **Live**, tap the toggle to mark yourself available.
- It **turns itself off automatically** after a while — by default 9 hours
  in India, 8 hours elsewhere, or a custom number of hours you can set on
  your **Profile** page. You don't need to remember to turn it off.
- Everyone on the team can see who's currently available and, if their
  timezone is set, what time it is where they are.

## 7. Manager Dashboard *(Web only, for now)*

If an admin on a *different* team has granted you **Manager** access to
their team (Settings → Managers, searchable by phone number), you'll see a
**Manager** tab showing that team's Live Availability — read-only, no
roster-edit rights come with it. This is meant for oversight across
multiple teams you don't otherwise work on directly.

Admins: grant or revoke this from your team's **Settings → Managers**
section. Revoking takes effect immediately.

## 8. Profile

- **Web**: set your timezone (auto-detected, editable) and your Live
  Availability auto-expiry override.
- **Mobile**: see your signed-in email, current team, switch teams, set a
  fallback **team member code** (only needed if the automatic link between
  your login and your roster row doesn't resolve on its own — normally you
  won't need to touch this), change your server address, and sign out.

## 9. Roles and permissions

| Role | Can view roster | Can edit roster | Can manage team members/settings |
|---|---|---|---|
| **Viewer** | Yes | No | No |
| **Editor** | Yes | Yes | No |
| **Admin** | Yes | Yes | Yes |

Plus two labels layered on top of Admin, not separate permission levels:
**Team Lead** and **Co-Lead**. And one entirely separate permission,
**Manager**, which is cross-team, read-only, and only covers Live
Availability (§7) — it doesn't touch roster editing at all.

## 10. Settings *(Web)*

Admin-only area covering:

- **Team settings** — organization name, budgeted team strength (shown for
  reference only, never enforced), default off-days, comp-off toggle.
- **Tracks & Subtracks** — how your roster groups team members.
- **Job Roles** and **Locations** — your team's own master lists, picked
  from a dropdown when adding/editing a team member rather than typed
  freely each time.
- **Shift Types** — the named shifts your roster offers (code, times,
  color).
- **Managers** — grant/revoke cross-team oversight (§7).

(A team holiday calendar exists as a backend API but isn't wired into the
Web Settings screen yet.)

Mobile currently exposes Tracks/Subtracks and Shift Types under its own
**Settings** tab; the rest are Web-only for now.

## 11. Reports *(Web)*

- **Utilization** — a date-ranged view of how much each team member worked.
- **Comp-offs** — everyone's pending (earned, not yet taken) and used
  comp-offs.

## 12. Import / Export *(Web)*

From the Roster page's Import/Export panel, bulk-import team members from a
spreadsheet, or export the current roster month to Excel/CSV (see §4).

## 13. Troubleshooting

- **"I don't see the shifts I expect"** — make sure you're on the right
  team (check the team switcher/Profile) and the right month.
- **Mobile: "Couldn't reach the server"** — double check the server address
  on the Profile tab; it needs to include `http://` and the port, e.g.
  `http://192.168.1.50:5080`. If you're testing on an Android emulator
  against a server running on the same machine, use `10.0.2.2` instead of
  `localhost`.
- **"I was added to a team but can't see it"** — sign out and sign back in;
  the very first login after being added is what links your account to
  your team membership.
- **"My Live Availability isn't showing"** — it auto-expires after your
  configured window (default 8–9 hours); toggle it back on if you're still
  around.
