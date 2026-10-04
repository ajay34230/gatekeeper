namespace XV.Core;

/// <summary>One chapter of the user guide: what the function is for, the steps to use it, and the screenshot that shows it.</summary>
public sealed record GuideSection(string Title, string Intro, string[] Steps, string[] Tips, string[] Shots);

/// <summary>The text of the built-in user guide. Shown in the Help tab and written into the PDF guide with the screenshots.</summary>
public static class GuideContent
{
    public const string Product = "XV Digital Access Control";
    public const string Maker = "Team XV";

    public static readonly GuideSection[] Sections =
    [
        new("1. The main window",
            "The Command Center is the server of your base. It keeps the encrypted records, talks to the gate phones and shows everything that happens at the gates.",
            [
                "Left side: live counts (people inside, vehicles on base, location flags, total events), the gate stations and the paired phones.",
                "Top buttons: Add Soldier, Add Vehicle, Import / Export, ID Card Studio, Visitors, Leave & Overdue, Local Wi-Fi & Pair Device, Comms, the lock and refresh buttons.",
                "Tabs: Live Feed, Personnel Registry, Vehicle Fleet, Transit Times, Accounts & Devices, Audit Trail, Export, Settings, Help and About.",
                "The search box at the top right filters the tab you are looking at.",
            ],
            ["The server keeps running when the window is locked."],
            ["01-live-feed"]),

        new("2. First-time setup (do this once)",
            "Before the gate phones can work, the server needs the posts of your base, an account for each Request Point (RP) and a paired phone.",
            [
                "Open Settings > Station & General > Open Station & General Settings (Stations & Settings).",
                "Under LOCATIONS type the ID (for example LOC07) and the name, then press Add / Update. Do the same for GATES (for example G02).",
                "Open the Accounts & Devices tab and press + Create Operator. Give the RP a name, an ID and a password.",
                "Press Pair Terminal (or the top button Local Wi-Fi & Pair Device). A QR code appears.",
                "On the phone open the XV Gatekeeper app, choose to pair, and scan the QR code. The phone appears under Paired Terminals.",
                "Add your people and vehicles (chapters 4 and 5), or import them from a file.",
            ],
            ["The phone and the PC must be on the same Wi-Fi for local pairing. For pairing over the internet use Cloud Link (chapter 17).",
             "Operators who register themselves on the phone must be approved in Accounts & Devices before they can sign in."],
            ["08-stations-settings", "06-pair-device"]),

        new("3. Live Feed",
            "Every gate scan arrives here at once, newest first. Each card shows who or which vehicle, where, when and who recorded it.",
            [
                "Use the filter chips: All Events, Personnel, Vehicles, Entries, Exits, Location Flags.",
                "A vehicle exit that has a destination shows a line such as 'To Location 08 - approx 30 min - ON THE WAY'. If the vehicle is late the line turns red: 'NOT REACHED'.",
                "A red Location Flag means the person or vehicle was scanned at a location other than the one it was expected at.",
                "Press Export CSV to save the feed as a spreadsheet.",
            ],
            ["Every card is also written to the Audit Trail and cannot be edited."],
            ["01-live-feed"]),

        new("4. Personnel Registry and soldiers",
            "The list of all soldiers and staff with their rank, number, unit, photo and card status.",
            [
                "Press + Add Soldier. Fill rank, name, army number, company, platoon, section, mobile and the other fields, add a photo, then save.",
                "To change a record, open the person from the registry and edit it. To see where the person has been, press History.",
                "To add many people at once press Import / Export, choose the soldier file (CSV or Excel) and import. The window tells you how many were added or updated and lists any rows with errors.",
                "To save the registry press Import / Export and export soldiers. You can choose all, one company or a selection.",
            ],
            ["A suspended person is refused at every gate until the status is made active again."],
            ["02-personnel", "09-add-soldier"]),

        new("5. Vehicle Fleet",
            "All registered vehicles with registration, type, model, company and status. A badge shows how many are on the way and how many have not reached.",
            [
                "Press + Add Vehicle and enter the registration, military registration, type, model, company and status.",
                "Open a vehicle to see its History: every entry and exit, the driver and co-driver, and its route chart.",
                "Import many vehicles from a CSV file with Import / Export.",
            ],
            ["The vehicle details appear at the top of every vehicle route chart and export."],
            ["03-vehicles"]),

        new("6. Transit Times - watching vehicles between locations",
            "When a vehicle leaves a gate the guard can enter where it is going and about how many minutes it should take. The server watches the clock. It never assumes the vehicle arrived: only a gate scan, the destination RP or this server can say so.",
            [
                "On the phone the guard enters the destination and approximate minutes when recording the vehicle exit. If the pair of locations already has a standard time the server uses it.",
                "Open the Transit Times tab. VEHICLES ON THE WAY lists every open trip with its due time.",
                "STANDARD TIME BETWEEN LOCATIONS: press + Time Between Locations to save a standard time for a pair of locations.",
                "AVERAGE TIME TAKEN shows the average, fastest and slowest time of all vehicles for each pair, and compares it with the standard.",
                "Press + Add Trip to record a trip by hand (a vehicle that left without a gate record).",
                "If a vehicle is late, a NOT REACHED pop-up appears on the server and the destination RP's phone gets a notice with the vehicle and the time it left.",
                "In the pop-up press Reached... and enter the time taken; or Stopped / moved... and enter the place and time taken; or the cross to close it.",
                "A closed pop-up comes back after 15 minutes with a sound until the trip is resolved.",
                "Press Export Transit Report for a PDF, Excel or CSV of all trips.",
            ],
            ["A scan of the vehicle at the destination closes the trip as REACHED. A scan at another location closes it as MOVED ELSEWHERE.",
             "The phone is only told the vehicle and the time it left. It never receives route times or the route chart."],
            ["03b-transit-times", "20-add-trip", "19-transit-alert", "18-close-trip"]),

        new("7. History and route chart",
            "Open History for any person or vehicle to see every record and a route chart: the places visited, the time spent at each, the travel between them and what happened on each trip.",
            [
                "Press History on a person or vehicle.",
                "Read the chart from top to bottom: LEFT, TRAVEL (with REACHED, NOT REACHED, MOVED ELSEWHERE or STOPPED), ARRIVED with the time stayed.",
                "For vehicles the driver and co-driver are shown, and a line tells you when the driver changed.",
                "Press Export Route... for a one-page PDF to print or a colour-coded Excel file.",
                "Press + Add Record in a person's history to add a missing entry or exit by hand, with the reason and remarks.",
            ],
            ["Entering a record by hand needs the administrator password if one is set, and is written to the audit trail."],
            ["17-vehicle-history-route", "11-add-history-record"]),

        new("8. Accounts & Devices",
            "The RP accounts that may sign in on the phones, and the phones that are paired.",
            [
                "+ Create Operator makes a new RP account.",
                "Disable stops an account from signing in. Reset Password sets a new password. Delete removes the account.",
                "Pair Terminal shows a new pairing QR code.",
                "Under PAIRED TERMINALS you can see each phone, its location and when it last reported. Press Revoke Access on a phone to stop it and order it to erase its data.",
            ],
            ["A revoked phone is told to wipe its stored data the next time it contacts the server."],
            ["04-accounts-devices"]),

        new("9. Audit Trail",
            "A tamper-evident log of every change, sign-in and gate record. Each entry is chained to the one before it.",
            [
                "Open the Audit Trail tab. Newest entries are first; the details are shown under each row.",
                "Press Verify integrity. The server checks the whole chain and tells you if anything was edited, removed or truncated.",
                "Press Export CSV to save the log.",
            ],
            ["Run Verify integrity regularly and before you hand over the server."],
            ["05-audit"]),

        new("10. Export tab and reports",
            "All exports are in one place: movement reports, route charts and the transit report. Each has its own card.",
            [
                "Open the Export tab.",
                "Choose the people, company or vehicles, the dates and the format (PDF, Excel or CSV), then press the export button of that card.",
                "The heading of reports can be changed in Settings (Export heading template).",
                "Route chart export has its own card, separate from the other exports.",
            ],
            ["Exports need the administrator password if one is set."],
            ["06-export", "10-reports-export"]),

        new("11. ID Card Studio and Card Register",
            "Design and print ID cards with a genuine QR code, and keep a record of every card issued, re-issued, printed or lost.",
            [
                "Press ID Card Studio. Choose the people (one, a company, or a selection), adjust the design and print or save as PDF.",
                "Open the Card Register to see each soldier's card number, issue date, last print and loss history.",
                "If a card is lost press Report lost... The old card is refused at every gate at once and a new QR code is made for the next card.",
                "Press Export company-wise... to save the register.",
            ],
            ["Personnel QR codes never expire. A code changes only when the card is reported lost or re-issued."],
            ["17-id-card-studio", "16-card-register"]),

        new("12. Visitors and temporary passes",
            "Day or temporary passes with a QR code that works only between 'valid from' and 'valid to'.",
            [
                "Press Visitors, then + New visitor pass.",
                "Enter the visitor's name, mobile, organisation, ID proof, purpose and whom they are visiting. Set the dates or use Today until 18:00, Next 24 hours or 7 days.",
                "Press Create pass (no print) to save the pass only, or Create & print pass to save it and open the printable pass.",
                "Later you can press Print pass on any visitor, End pass now to stop a pass, or History to see where the visitor went.",
                "Press Export visitors report... for a spreadsheet.",
            ],
            ["Exits are always allowed, so nobody is trapped inside. A visitor still inside after the pass ends is flagged as overstay."],
            ["13-visitors", "14-new-visitor-pass"]),

        new("13. Leave, TD and overdue alerts",
            "People who left with an expected return date are tracked. When the date passes the server raises an alert.",
            [
                "The phone asks for an expected return date for the reasons you choose in Stations & Settings (for example Proceeding on Leave, TD).",
                "Press Leave & Overdue to see everyone who is out. Use the chips Everyone out or Overdue only.",
                "When someone is overdue, a pop-up appears on the PC: for example 'Havildar Name has not returned from leave', with the reason and the due date.",
                "Any entry of that person closes the absence automatically.",
                "Settings > Leave & Overdue Alerts: choose which Locations or RPs also get the alert on their phone. Only those phones receive it.",
            ],
            ["By default the alert is shown on the PC only."],
            ["15-leave-overdue", "21-not-returned-alert"]),

        new("14. Comms Center",
            "Messages, alerts, calls and SOS between the server and the paired phones.",
            [
                "Press Comms and choose a terminal on the left.",
                "Type a message and press Send, or press Send ALERT to ring the phone even when its app is in the background.",
                "Broadcast alert to all terminals... sends one alert to every phone.",
                "An SOS from a phone appears on the PC at once with the sender and the place.",
                "Voice and video calls with a terminal are listed in the conversation, with missed calls in red.",
            ],
            ["Messages are encrypted end to end."],
            ["12-comms-center"]),

        new("15. Settings",
            "Everything that controls how the server behaves.",
            [
                "QUICK STATUS shows the current data sharing, internet access, auto-lock, outbound block, self-registration, administrator password and heading template.",
                "Station & General: locations, gates, server name, port, custom fields, movement reasons, data protection, diagnostics and maintenance.",
                "Cloud Link: lets phones reach the PC over the internet (chapter 17).",
                "Leave & Overdue Alerts: who else receives the leave alert on the phone.",
                "Press Save Settings after any change.",
            ],
            ["If you change the HTTPS port, the phones must be paired again."],
            ["07-settings", "08-stations-settings"]),

        new("16. Backup, restore, lock and administrator password",
            "Protect and recover your data.",
            [
                "Import / Export > ENCRYPTED BACKUP & RESTORE > Create encrypted backup... Choose a file and a password. The file holds the whole database and settings, encrypted.",
                "Restore from backup... asks for the same password and replaces the data on this PC. Do this only on purpose.",
                "Set an administrator password in Settings. It is then asked before exports, manual records and other sensitive actions.",
                "Press the lock button at the top to lock the screen. The gate server keeps running. Enter the administrator password to unlock.",
                "Auto-lock locks the screen after the idle time set in Settings.",
            ],
            ["Keep the backup password safe. Without it the backup cannot be opened."],
            []),

        new("17. Cloud Link (phones over the internet)",
            "Lets a phone outside the base Wi-Fi reach the server through a secure tunnel.",
            [
                "Open Settings > Cloud Link.",
                "Enter the secure web addresses (URLs) of the gate server and the comms server given by your tunnel.",
                "Pair the phone again with the new QR code. The phone then works on mobile data too.",
                "Turn internet access off again to return to local Wi-Fi only.",
            ],
            ["All traffic is encrypted (TLS with AES-256-GCM messages) even over the internet."],
            ["07-cloud-link"]),

        new("18. The phone app (for the RP at the gate)",
            "The XV Gatekeeper app on the phone is used by the RP to record entries and exits.",
            [
                "Sign in with the RP account made in Accounts & Devices, and choose the location and gate.",
                "Scan a card QR. The app shows the person and asks Entry or Exit. For an exit choose the reason; for leave or TD enter the expected return date.",
                "For a vehicle, scan the vehicle QR, then the driver and everyone on board, and confirm. On a vehicle exit enter the destination and the approximate minutes.",
                "If the destination is a location of your base, the RP there is told when the vehicle has not reached. The RP can then press Reached..., enter the time taken, or say it stopped or moved to another place.",
                "Records made without network are kept on the phone and sent when the server is reachable again.",
                "The SOS button sends an emergency alert with the position to the server.",
                "Shift handover passes unsent records to the next phone over Bluetooth or a Wi-Fi hotspot.",
                "Language (English or Hindi) and dark mode are in the app's settings.",
            ],
            ["If a phone is lost, revoke it in Accounts & Devices. It erases its data on the next contact."],
            []),

        new("19. If something does not work",
            "Quick checks.",
            [
                "A phone cannot connect: check the PC and phone are on the same Wi-Fi, the PC firewall allows the app, and the phone is not revoked.",
                "A phone shows the wrong time or is refused: set the correct date and time on the phone.",
                "A scan is refused: open the person in the Personnel Registry and check the status is active and the card was not reported lost.",
                "A pop-up does not come back: open the Transit Times tab and resolve or snooze the trip.",
                "Anything unexpected: look at the Audit Trail for the exact entry and time.",
            ],
            [],
            []),
    ];
}
