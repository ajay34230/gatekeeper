# XV Digital Access Control

Entry/exit control for personnel and vehicles at base gates.

| Part | What it is | Where |
|---|---|---|
| **XV Command Center** (Windows) | Native Windows app (C# / WPF) with the encrypted server built in. HQ dashboard, personnel & vehicle registry, ID cards with QR, operator accounts, paired terminals, audit trail, Cloud Link. | `windows/` |
| **XV Gatekeeper** (Android) | Native Android app (Kotlin / Jetpack Compose). Scans personnel and vehicle QR credentials with the camera, works offline, syncs securely to the Command Center. | `android/` |

Both apps start **empty** — there is no demo or sample data. You add your own locations, gates, personnel, vehicles and operators on the PC.

---

## 1. Download

Every push builds both apps on GitHub:

1. Open the repository on GitHub → **Actions** → the latest **"Build APK and Windows installer"** run.
2. Scroll to **Artifacts** and download:
   - `XV-CommandCenter-Setup` → Windows installer (`XV-CommandCenter-Setup-1.0.0.exe`)
   - `XV-Gatekeeper-APK` → Android app (`app-debug.apk`)
   - `windows-screenshots` → screenshots of the Windows app taken on GitHub's Windows machine

## 2. Install on the PC (Windows 10/11, 64-bit)

1. Run `XV-CommandCenter-Setup-1.0.0.exe` → *Next* → *Install*. (Windows SmartScreen may warn because the installer is not code-signed yet: *More info* → *Run anyway*.)
2. The installer adds Start-menu and desktop shortcuts and opens **Windows Firewall** for TCP 8443 (encrypted API) and UDP 47913 (terminal discovery on the LAN).
3. Start **XV Command Center**. The secure server starts automatically with the app (and with Windows, unless you turn that off in *Stations & Settings*).

First-time setup on the PC:

1. **Stations & Settings** → add your locations (e.g. `LOC07` "Location 07 (Main Hub)") and gates (e.g. `G02` "Gate 02 (Primary)").
2. **+ Add Soldier** / **+ Add Vehicle**, or **Import / Export** → *Blank template* → fill in Excel → *Import CSV*.
3. **Accounts & Devices** tab → **+ Create Operator** for each gatekeeper (or let them use *Create Account* in the app and approve them here).
4. For each person/vehicle: **ID Card & QR** / **Windshield QR** → *Print* or *Save PNG*.

## 3. Install on the phone (Android 8+)

1. Copy `app-debug.apk` to the phone and open it (allow *Install unknown apps* for your file manager when asked).
2. Open **XV DIGITAL ACCESS CONTROL** → tap **PC Server Connection** → **SCAN PC PAIRING QR**.
3. On the PC click **Local Wi-Fi & Pair Device** and scan the QR shown there. (Phone and PC must be on the same Wi-Fi/router for this first pairing, unless Cloud Link is configured.)
4. Choose your **Station Location** and **Active Gate**, sign in with the operator ID and password.

Building it yourself instead: `cd android && ./gradlew :app:assembleDebug` (JDK 17, Android SDK 35) → `app/build/outputs/apk/debug/`.

## 4. Security

- **In transit:** HTTPS (TLS 1.2/1.3) to the PC's own certificate, which the phone **pins** by its SHA-256 fingerprint from the pairing QR — a fake server is rejected. Inside TLS, every request and response is additionally sealed with **AES-256-GCM** using a key unique to that phone (issued once at pairing), with timestamp + nonce **replay protection**. Data therefore stays end-to-end encrypted even if a cloud tunnel or relay handles TLS.
- **At rest (PC):** SQLCipher-encrypted database; the key is protected by Windows DPAPI for that machine.
- **At rest (phone):** SQLCipher-encrypted database; its passphrase and all tokens/keys are wrapped by the Android Keystore.
- **Credentials:** QR codes contain a random secret code (not the ID), so a badge cannot be forged by typing an ID; *Re-issue QR* invalidates a lost card. Operators sign in with passwords (PBKDF2, lockout after 5 failures); terminals can be revoked instantly from the PC.
- When *Cloud Link* internet access is **off**, the PC refuses every connection that is not from a private/LAN/VPN address.
- **Data sharing (Stations & Settings → Data protection):** the PC only answers requests; it never pushes data anywhere. The administrator chooses what terminals may receive:
  - *Full* — names, ranks, units and badge secrets are copied to terminals (fully offline scanning).
  - *Minimal* (default) — terminals keep only IDs, status and SHA-256 hashes of badge codes; names are fetched for the screen during an online check and never stored.
  - *Receive-only* — terminals store no registry at all; every scan is verified online (`credential.verify`). Gate records still flow phone → PC.
- **Outbound block:** the installer adds a Windows Firewall rule that stops the Command Center from opening connections to public internet addresses (LAN, VPN `100.64.0.0/10` and IPv6 unique-local stay allowed). It can be switched off in *Data protection* (administrator approval required).
- **Administrator password:** every export (reports, CSV, connection details), credential save/print, record wipe and data-protection change asks for the administrator password (set on first use, PBKDF2-hashed, unlock remembered for 5 minutes). Each use and each failed attempt is written to the Audit Trail.

## Soldier register & ID Card Studio

- **Soldier details** (Personnel Registry → Add / Edit): army no, rank, name, appointment, unit, company, platoon, section, blood group, mobile, address, date of birth, enrolment, card expiry, identification mark, next of kin, card serial, photo — plus any **custom fields** you add in *Stations & Settings → Customisation*.
- **Import / Export → Soldier register**: import an **Excel (.xlsx) or CSV** file (download the blank template for the exact columns; custom fields appear as extra columns, and unknown columns can be added as new custom fields during import). Export all details for **one or several soldiers, a company, a platoon, a section or everyone**. *Import photos from folder* matches `P001.jpg` / `<Army No>.jpg`.
- **ID Card Studio** (header button, or *ID Cards* in Personnel Registry for the ticked soldiers): the soldier ID card design, filled from the encrypted register. Select **individual, multiple, company, platoon, section or entire** register; customise every text line, the conditions on the back, the issuing authority and signature, the colour theme and your own crest / unit badge; add each soldier's signature by drawing it or uploading a scan / digital signature image (the paper background is removed automatically); then **print** or **save a PDF** at true card size (85.6 × 54 mm, front + back with cut marks). The QR on each card is the soldier's own secret code, so the printed card is the credential the gate terminals scan. The machine-readable lines on the back are computed from the soldier's details (ICAO 9303 check digits) and the "CHECK" value is a SHA-256 fingerprint of the printed details.
- **Entry / exit reasons**: the terminal asks for a reason (TD, Proceeding on Leave, Rejoining from Leave, Posting Out, Posting In, Local Work, or a custom reason) and remarks; the list is edited in *Stations & Settings*. Reason and remarks are stored with the record and appear in the live feed, history and all reports.

## Comms (messages & alerts)

The **Comms** button on the PC opens the Comms Center: pick a paired terminal, send a message or an **ALERT**, or broadcast an alert to every terminal. On the phone the **Comms** tab shows the conversation and can send messages or alerts back; alerts from phones pop up on the PC with a sound, and alerts from the PC ring on the phone's alarm channel even when the app is closed.
**Voice and video calls** work both ways: *Voice call* / *Video call* in the Comms Center (PC) or on the phone's Comms tab. The phone rings like a normal call (full-screen, even when locked); the PC shows a ringing pop-up. Media goes directly between the two devices, encrypted with DTLS-SRTP, and no external servers are used — so calls work on the same network or over a VPN such as Tailscale (not through plain port forwarding). The PC needs the Microsoft Edge WebView2 Runtime (built into Windows 11 and current Windows 10) and a microphone/camera; without them you can still hear/see the phone.
Comms is a separate engine: its own port (8444), its own encrypted databases on both sides and its own key per terminal, so it never touches the gate records. Messages to an offline terminal are queued and delivered when it reconnects. For reliable alerts, allow the app to run in the background (the Comms tab offers a shortcut to the battery setting). For internet use, forward/tunnel port 8444 as well (see Cloud Link).

## 5. Connecting over the internet (Cloud Link)

Open **Cloud Link** on the PC. It lists every detail a remote connection needs (server ID, ports, LAN addresses, certificate fingerprint, endpoints) and supports:

| Method | When to use |
|---|---|
| **VPN** (Tailscale / ZeroTier) — recommended | No router changes. Install on PC and phones, enter the PC's VPN IP as public host. |
| **Port forwarding + DDNS** | Router forwards TCP 8443 to the PC; use a static IP or free DDNS name. |
| **Tunnel** (Cloudflare Tunnel / ngrok) | Paste the tunnel's `https://` URL and tick "public certificate". |
| **Relay** | A cloud VM forwards TCP to the PC. |

After saving, pair (or re-pair) phones — the QR then carries the internet address. On the phone, **Sync Hub → Sync Target Architecture** chooses *Auto* (LAN first, internet when away), *Local PC Wi-Fi* or *Cloud Server*.

## 6. Repository layout

```
android/                  Native Android app (Gradle project)
windows/src/XV.Core       Server: encrypted DB, business rules, HTTPS API, discovery, pairing
windows/src/XV.CommandCenter  Windows UI (WPF) hosting the server
windows/src/XV.Server     Same server without UI (automated tests / headless use)
windows/installer         NSIS installer script
windows/tests             End-to-end protocol test (acts exactly like a phone)
docs/PROTOCOL.md          Wire protocol
reference/                The React reference UI the native apps were built to match
```
