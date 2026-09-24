# XV DIGITAL ACCESS CONTROL

Field entry/exit control for personnel and vehicles: QR scanning at the gate, a central server, and a PC Command Center.

**Goal for whoever continues this (Claude or a person):** make the native Android app, the Windows software and the server all look and behave like the initial zip (`gatekeeper-entry_exit-system`) - same UI design, full functionality, **no fake or demo data anywhere**, every part really connected to the server.

This file records what was done so far, what is verified, and what is still open. Read "Open work" first.

---

## 1. Project layout

| Path | What it is |
|---|---|
| `src/`, `index.html`, `vite.config.ts`, `server.ts`, `server/` | The **initial zip's** system: React/Vite web app (mobile gatekeeper terminal + PC Command Center) and the Node/Express server. |
| `android/` | Capacitor 6 wrapper that turns the React app into an APK (`com.gatekeeper.entryexit`). |
| `TeamXV_QR_Monitoring_Prototype/app/` | **Native Android app** (Kotlin + Jetpack Compose, Room, CameraX + ML Kit real QR scan, WorkManager sync). |
| `TeamXV_QR_Monitoring_Prototype/server/` | Alternative **Python/Flask + SQLite** central server with its own admin web UI (port 8000). |
| `TeamXV_QR_Monitoring_Prototype/pc_native_c/` | **Native Windows** desktop console in C (Win32, WinHTTP). |
| `electron/` | Empty in the original zip (`main.cjs`, `preload.cjs` are 0 bytes) - no Windows installer can be produced from this yet. |
| `data/` | Runtime data (accounts, registry, records). Not committed; created at run time. |

There are **two server stacks that do not share an API**: Express (`/api/...`, port 3000, used by the React app) and Flask (`/api/v1/...`, port 8000, used by the Kotlin app and the C app). Section 4 describes the bridge that lets Express also serve the Kotlin app.

## 2. Product name

Display name is **XV DIGITAL ACCESS CONTROL**. It was changed in page titles, manifests, Android labels, login screens, server messages, report headers, download names and installer text.
Deliberately **not** renamed (code identifiers): `gatekeeperId`, `GatekeeperSession`, Android package ids (`com.gatekeeper.entryexit`, `com.teamxv.qrmonitor`), `TEAMXV_*` env-var names, browser storage key names, and job-role wording ("Gatekeeper ID", "Field Gatekeeper").

## 3. What was done, and its status

### Web app + Express server (root project)
- Restored the full initial zip; fixed the only build blockers: `esbuild` pinned `^0.28.0` (was `^0.25.0`, conflicts with Vite 8) and a missing `Trash2` import in `PcCommandCenter.tsx`. `npx tsc --noEmit` = **0 errors**; `vite build` OK.
- **Fake/demo data removed** from the app: no default operator ("Marcus Vance"), no pre-filled login, app starts **offline** and only shows *online* when `/api/sync/ping` answers, real personnel/vehicles are loaded from the server, sign-in required, storage keys bumped to `_v2` so old demo data in a phone is ignored. Relative `/api/...` calls now go through `apiUrl()` (`src/utils/networkSync.ts`) so the installed app reaches the PC by its configured address.
- **Accounts** (`server/accounts.ts`, `server/adminPage.ts`): scrypt-hashed passwords, 30-day tokens, lockout after 5 failed logins, self sign-up rate limit. Admin page `http://localhost:3000/admin/accounts` (PC only) creates accounts with generated passwords, resets, disables, approves. App login screen has Sign In / Create Account and a "PC Server Connection" box. Tested with real calls (create, duplicate 409, login, 429 lockout, sign-up, weak password) and in a browser.
  Env: `XV_REQUIRE_APPROVAL=1` sign-ups wait for approval; `XV_ADMIN_ANY_HOST=1` allows admin actions from other LAN PCs.
- **Capacitor APK** built and installed on a Galaxy S10 earlier; showed clean offline login (no demo data). Not re-tested after later changes.

### Native Android app (`TeamXV_QR_Monitoring_Prototype/app`)
- Never compiled before (see its `HANDOVER_CLAUDE.md`). Fixed to **BUILD SUCCESSFUL** (`gradle :app:assembleDebug`, Gradle 8.9, JDK 17, compileSdk 35): CameraX pinned to 1.4.2 (1.6.2 needs a newer Android Gradle Plugin); missing imports; wrong import paths (`KeyboardOptions`, `Barcode`); `Icons.Truck` -> `LocalShipping`; added `SessionRouter`, `PresenceStatus`/`PersonPresence`, `observePersonnel()`, `observeAttentionCount()`; `MlKitAnalyzer.getResult` -> `getValue`; renamed clashing `setSoundEnabled`.
- **`seedDemoData()` removed** (it inserted 20 fake persons and 10 fake vehicles on every start). The app now gets people/vehicles from the server (`/api/v1/master/bootstrap`).
- Label and login title changed to XV DIGITAL ACCESS CONTROL.
- **Not tested on a device yet.** The APK was built but not installed/run.

### Python server (`TeamXV_QR_Monitoring_Prototype/server`)
- Installed (`.venv`, Flask 3.1, qrcode), runs, `/api/v1/health` OK.
- **Bugs fixed:** `seed_admin()` was never called (no admin could ever exist); default password `TeamXV-ChangeMe-2026!` and a login-page hint showing it removed - first run now creates the admin with a **random password saved to `ADMIN_FIRST_RUN.txt`** (delete it after use); `seed_master()` no longer inserts placeholder persons/vehicles (locations/gates 01-10 are still created).
- The shipped `teamxv.db` (24 placeholder persons, 11 vehicles, a test event) was moved aside; a clean DB is created on start.
- Already has operator login, admin user creation, QR credential printing, reports, dashboard.

### Native Windows app (`pc_native_c`)
- Compiles with w64devkit GCC (`build_x64_gcc.cmd`; two MinGW portability fixes: `<shellapi.h>`, `WINHTTP_NO_REFERRER`). Runs and shows ONLINE against the Python server.
- **Still shows fake data:** `PopulateSampleRows()` in `src/main_window.c` fills every tab with hardcoded rows, and `MainWindow_OnPaint` falls back to fixed KPI numbers 42/24/18/19/8. The look is a plain Win32 window, **not** the zip's design.
- Compiler is not in the repo: download w64devkit (`skeeto/w64devkit` releases) into `tools/w64devkit`.

### Express <-> native app bridge (written, type-checks, **not run-tested**)
- `server/apiV1.ts` mounted at `/api/v1`: `health`, `auth/login|register|logout|me`, `master/bootstrap`, `heartbeat`, `devices`, `events`, `events/batch`, `sync/batch`, `sync/vehicle-transaction` - shapes match `network/ApiClient.kt`. `XV_REQUIRE_AUTH=1` makes data endpoints require a signed-in token (off by default).
- `server/militaryRegistry.ts`: personnel/vehicle registries now **persist** to `data/personnel.json` / `data/vehicles.json` (they were memory-only); vehicle upsert/remove added.
- `server/routes.ts`: `POST/PATCH/DELETE /api/vehicles`, `DELETE /api/personnel/:id`, and `/api/gates/status` now computed from real device heartbeats and records (was a hardcoded fake list). `server/devices.ts` stores heartbeats.

## 4. Open work (priority order)

1. **Test the bridge end to end**: start Express, log the Kotlin app in, bootstrap, scan, confirm the record appears in the PC Command Center. Nothing here has been run.
2. **Kotlin QR parser** (`scanner/QrPayloadParser.kt`) only accepts `P001` / `V014`. The zip's ID cards encode `P-001|Location 07|SEC-P001-ALPHA` (or the secret code). Accept the first `|` segment and match secret codes; bootstrap ids are sent dash-less (`P001`).
3. **Kotlin login screen**: add a server address field (currently `BuildConfig.DEFAULT_SERVER_HOST` `192.168.1.10:8000`, editable only by admins in Settings) and a Create Account mode calling `POST /api/v1/auth/register` (server side exists). Improve the 403 message (pending approval / disabled).
4. **PC Command Center**: `src/screens/PcCommandCenter.tsx` (~lines 459-466) still falls back to five fake gates when the server returns none - show an empty state instead. Add a vehicle registration form (`VehicleFleetModal` is read-only; the server endpoint `POST /api/vehicles` exists).
5. **Windows software**: either wire `pc_native_c` to real data (needs a JSON parser; endpoints `/api/v1/summary/daily`, `/events`, `/presence`, `/persons`, `/vehicles` on the Flask server) and remove the sample rows, or ship a small native launcher that opens the React PC Command Center (which matches the zip's design). Empty `electron/` files mean no installer yet.
6. Capacitor app: the scanner opens the camera but does **not decode QR codes** (manual ID entry only). Native Kotlin app does decode (ML Kit).
7. Decide on one server: Express (zip's UI) or Flask. The plan was Express as the single server for both the PC UI and the Kotlin app.
8. Security: the Express API is open unless `XV_REQUIRE_AUTH=1`; Python API is `prototype-open` unless `TEAMXV_REQUIRE_*` are set. Use HTTPS and device keys before real deployment. Capacitor debug APK allows cleartext HTTP.

## 5. How to run

```bash
# Express server + web/PC Command Center  (Node 20+)
npm install
npm run build                       # vite build (empties dist/)
npx esbuild server.ts --bundle --platform=node --format=cjs --packages=external --outfile=dist/server.cjs
NODE_ENV=production node dist/server.cjs      # http://localhost:3000   admin: /admin/accounts
# dev: npm run dev

# Web -> APK (Capacitor 6, JDK 17, Android SDK 35)
npx cap sync android && cd android && ./gradlew assembleDebug

# Native Android app (Kotlin) - Gradle 8.9, JDK 17, compileSdk 35
cd TeamXV_QR_Monitoring_Prototype && gradle :app:assembleDebug     # needs local.properties sdk.dir

# Python server
cd TeamXV_QR_Monitoring_Prototype/server && python -m venv .venv && .venv/Scripts/pip install -r requirements.txt && .venv/Scripts/python app.py   # port 8000

# Windows native console (needs w64devkit in tools/)
cd TeamXV_QR_Monitoring_Prototype/pc_native_c && build_x64_gcc.cmd
```

Notes: Windows PowerShell 5.1 writes a BOM with `Set-Content -Encoding utf8`, which breaks `package.json` - write UTF-8 without BOM. On this PC `npm install` had transient network resets; retry.

## 6. Mistakes to avoid
- An early overwrite of a newer working tree with the zip's files lost changes (recovered by re-importing the newer zip). Keep backups before bulk copies.
- Do not trust the prototype's docs: `HANDOVER_CLAUDE.md` claims stabilisation but the Kotlin code did not compile.
