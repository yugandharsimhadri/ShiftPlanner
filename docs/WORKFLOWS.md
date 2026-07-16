# ShiftPlanner — Workflow Diagrams

Companion to the [Technical Design Document](TECHNICAL_DESIGN.md). These
diagrams walk through the system's key request flows and feature lifecycles.

## 1. Sign-up, login, and team selection

```mermaid
sequenceDiagram
    actor U as User
    participant C as Client (Web/Mobile)
    participant API as Backend API
    participant DB as Database

    U->>C: Enter email/phone + password
    C->>API: POST /api/login (or /api/login-phone)
    API->>DB: Verify credentials (ASP.NET Identity)
    DB-->>API: OK
    API-->>C: Bearer token
    C->>API: GET /api/teams/mine (Authorization: Bearer ...)
    API->>DB: PendingInviteClaimer.ClaimAsync\n(match email/phone to un-claimed Person rows)
    API->>DB: Find TeamMember rows for this user
    DB-->>API: List of teams + role in each
    API-->>C: Teams list

    alt Zero teams
        C->>U: Show "Create a team" form
        U->>C: Team name
        C->>API: POST /api/teams
        API-->>C: New team (caller becomes Admin)
    else One team
        C->>C: Auto-select it
    else Multiple teams
        C->>U: Show team picker
        U->>C: Pick one
    end

    C->>C: Store team id/name/role locally
    C->>U: Land on Roster
```

**Key point:** `PendingInviteClaimer` is what lets an admin add someone by
email/phone *before* that person has ever signed up — the very first time
they log in with a matching email or phone, their pre-created `Person` row
gets linked to their new login, and their `TeamMember` rows (and any
`ManagerAssignment` rows) just appear.

## 2. Every tenant-scoped request

```mermaid
sequenceDiagram
    participant C as Client
    participant F as RequireTeamFilter
    participant EP as Endpoint handler
    participant DB as Database

    C->>F: Request + Authorization: Bearer ... + X-Team-Id: 7
    F->>DB: Resolve UserId from token
    alt No valid token
        F-->>C: 401 Unauthorized
    end
    F->>DB: PendingInviteClaimer.ClaimAsync
    F->>F: Read X-Team-Id header
    alt Missing/invalid header
        F-->>C: 400 Bad Request
    end
    F->>DB: Find TeamMember where TeamId=7 AND Person.UserId=caller
    alt No matching TeamMember
        F-->>C: 403 Forbidden
    else Role below endpoint's minimum
        F-->>C: 403 Forbidden
    else OK
        F->>F: Stash TeamContext {UserId, TeamId, Role} on HttpContext
        F->>EP: next()
        EP->>DB: Query/mutate, always scoped by TeamContext.TeamId
        DB-->>EP: Result
        EP-->>C: 200 OK
    end
```

Every tenant-scoped read or write in the API goes through this exact
sequence — a client can never smuggle in a different `TeamId` inside a
request body, because handlers only ever read `TeamId` from the
server-resolved `TeamContext`, never from client input.

## 3. Assigning a shift

```mermaid
flowchart TD
    A["Editor/Admin opens Roster,\ntaps a team member's day cell"] --> B["Pick a shift code\n(or Clear)"]
    B --> C["PUT /api/roster/entry\n{teamMemberId, date, shiftCode}"]
    C --> D{"RequireTeamEditor\npasses?"}
    D -->|No| E["403 Forbidden"]
    D -->|Yes| F["Upsert RosterEntry"]
    F --> G["CompOffAutoEarn.SyncAsync"]
    G --> H{"CompOffsEnabled AND\ndefault-off day AND\nshift IsWorkShift?"}
    H -->|Yes, no pending entry yet| I["Create CompOffEntry\n(Status = Pending)"]
    H -->|No, but a Pending entry\nexists for this date| J["Remove that Pending entry\n(Used entries are left alone)"]
    H -->|No change needed| K["Done"]
    I --> K
    J --> K
    K --> L["Client reloads the month,\nrenders updated chip"]
```

## 4. Copy Forward

```mermaid
flowchart TD
    A["Editor/Admin picks a source month\nand a target month"] --> B["Choose pattern:\nweekday or exact-date"]
    B --> C["POST /api/roster/copy-forward"]
    C --> D["For each source entry"]
    D --> E{"Target team member\nis Active?"}
    E -->|No, inactive| F["Skip — do not copy"]
    E -->|Yes| G{"Target date already has\nan entry / holiday / leave?"}
    G -->|Conflict| H["Flag it — do not overwrite,\nreturned in response.Flagged"]
    G -->|Clear| I["Create RosterEntry\nSource = Copied"]
    F --> J["Response: CopiedCount + Flagged[]"]
    H --> J
    I --> J
    J --> K["Client shows summary,\nlists flagged rows for human review"]
```

## 5. Comp-off lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending: Auto-earned when a shift\nis assigned on a default-off day\n(CompOffsEnabled = true)
    Pending --> Used: POST /{id}/use\n(pick a make-up date)
    Used --> Pending: POST /{id}/unuse\n(reverses the use)
    Pending --> [*]: Never used\n(still counted as owed\nin utilization reports)
    Used --> [*]: Fully settled
```

## 6. Live Availability

```mermaid
sequenceDiagram
    actor M as Team member
    participant C as Client
    participant API as Backend
    participant Svc as AvailabilityService

    M->>C: Tap "I'm available"
    C->>API: PATCH /members/me/availability {isAvailable: true}
    API->>API: IsAvailable = true\nAvailableSince = now
    API-->>C: 200 OK

    Note over C,API: Later — anyone on the team opens Live tab
    C->>API: GET /api/teams/current/availability
    API->>Svc: For each member: IsEffectivelyAvailable(member, person, now)
    Svc->>Svc: expiryHours = person.Override\n?? (IsIndiaTz(person.Timezone) ? 9 : 8)
    Svc-->>API: true if IsAvailable AND\n(now - AvailableSince) < expiryHours
    API-->>C: List of members with effective status\n(never mutates stale flags)
```

The expiry is computed on every read, never written back — so a member who
forgets to toggle off simply stops showing as available once their window
elapses, with no cleanup job required.

## 7. Manager oversight — grant and cross-team dashboard

```mermaid
sequenceDiagram
    actor Admin as Team Admin
    actor Mgr as Manager (outside the team)
    participant API as Backend
    participant DB as Database

    Admin->>API: GET /api/teams/current/managers/search?phone=...
    API->>DB: Search people the admin already manages elsewhere
    DB-->>API: Candidate Person(s)
    API-->>Admin: Results
    Admin->>API: POST /api/teams/current/managers {personId}
    API->>DB: Create ManagerAssignment {PersonId, TeamId, GrantedByUserId}
    API-->>Admin: Confirmed

    Note over Mgr,API: Manager's own session — no X-Team-Id used here
    Mgr->>API: GET /api/manager/teams
    API->>DB: All ManagerAssignment rows for this person
    DB-->>API: List of teams
    Mgr->>API: GET /api/manager/availability
    API->>DB: Availability across every assigned team
    API-->>Mgr: Combined cross-team dashboard

    Admin->>API: DELETE /api/teams/current/managers/{id}
    API->>DB: Remove ManagerAssignment
    Note over Mgr,API: Manager immediately loses visibility\ninto that team on next request
```

## 8. New team member onboarding

```mermaid
flowchart TD
    A["Admin adds a team member\n(name, phone, code, track, ...)"] --> B["POST /api/teams/current/members"]
    B --> C{"Person with this\nemail/phone exists?"}
    C -->|Yes| D["Reuse existing Person\n(link new TeamMember to it)"]
    C -->|No| E["Create new Person\n(UserId = null — no login yet)"]
    D --> F["TeamMember created,\nAccessRole set (default Viewer)"]
    E --> F
    F --> G["Member has a roster row\nbut may not be able to log in yet"]
    G --> H{"They sign up/log in\nwith matching email/phone?"}
    H -->|Not yet| G
    H -->|Yes| I["PendingInviteClaimer links\nPerson.UserId to their login"]
    I --> J["All their TeamMember rows\n(across every team) become\nimmediately usable"]
```
