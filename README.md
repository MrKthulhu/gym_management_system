1. Project Overview
A small gym needs a simple membership and training management system with day-to-day attendance. This app does exactly that.
2. How to Run (MAUI Desktop)
Prereqs: .NET 8 SDK, Windows.
Steps:
1) Open solution in Visual Studio.
2) Set startup project to the MAUI app.
3) Configure connection string in appsettings or user secrets.
4) Run (Windows Machine).

3. Pages (Screens & Routes)
List each page and what it does:
•	- /add-member — create member with plan (age mandatory, email unique, creates membership + pending payment).
•	- /view-members — list members + latest membership details.
•	- /add-trainer — add trainer (name, specialization).
•	- /assign-trainer — assign/unassign trainer to member (ACTIVE members only).
•	- /attendance — per-day session attendance (MDT), dedupe, today's + recent.
•	- /view-trainers — rank trainers by active members.

Route	Title	Purpose
/Add-member	Add Member	Create member, choose plan (age required).
