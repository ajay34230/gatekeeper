# Terminal ↔ Command Center protocol (v1)

Transport: HTTPS on port 8443 (TLS 1.2/1.3). The server certificate is self-signed; terminals pin its SHA-256 fingerprint.

## Pairing QR

`XVGK1:` + base64url(JSON):

```json
{"id":"XV-1A2B3C4D","n":"HQ-PC","h":["192.168.1.20"],"p":8443,"u":"https://gate.example.org:8443","pc":false,"fp":"<sha256 hex>","c":"K7M2Q9XZ"}
```

`h` LAN addresses · `p` port · `u` internet URL (optional) · `pc` internet URL uses a public CA certificate · `fp` certificate fingerprint · `c` one-time code (10 minutes).

## Endpoints

| Method | Path | Body |
|---|---|---|
| GET | `/api/v1/ping` | – (public identity only) |
| POST | `/api/v1/pair/enroll` | `{"code","deviceName","model"}` → `{"deviceId","deviceKey"(base64, 32 bytes),"serverId","serverName","fingerprint"}` |
| POST | `/api/v1/rpc` | encrypted envelope (below) |

## Envelope

Headers: `X-GK-Device`, `X-GK-Ts` (ms), `X-GK-Nonce` (random, 16 bytes base64url).
Body: `{"iv": base64(12 bytes), "ct": base64(ciphertext ‖ 16-byte tag)}` — AES-256-GCM with the device key.

- Request AAD: `XVGK1|req|{deviceId}|{ts}|{nonce}`, plaintext `{"op": "...", "data": {...}, "token": "<operator token>|null"}`
- Response AAD: `XVGK1|res|{deviceId}|{nonce}`, plaintext `{"code": <http-like status>, "body": {...}}`
- Rejected: unknown/revoked device (401), clock skew > 10 min or repeated nonce (401), bad tag (400).

## Operations

| op | token | data |
|---|---|---|
| `health` | no | – |
| `stations.list` | no | – → locations, gates, `reasons` (entry/exit reasons set on the PC) |
| `auth.login` | no | `username`, `password` |
| `auth.register` | no | `name`, `username` (optional), `password` → `pending` until approved on the PC |
| `auth.logout` | yes | – |
| `master.bootstrap` | yes | – → `sharingMode`, `reasons`, persons, vehicles, locations, gates, presence, active `manifests` (see below) |
| `credential.verify` | yes | `code` (badge secret, ID or service number / plate), `expected` (`PERSON`/`VEHICLE`/empty) → details for this one scan; 404 `NOT_REGISTERED` |
| `events.create` | yes | person movement event; optional `reason` (≤ 60 chars, from `reasons` or typed) and `remarks` (≤ 300 chars) |
| `vehicle.transaction` | yes | `{event, manifest}` vehicle entry/exit with occupants |
| `heartbeat` | yes | `locationId`, `gateId`, `operatorId`, `pending`, `appVersion` |

### Data sharing modes

`sharingMode` is set on the PC by the administrator:

| mode | persons / vehicles in bootstrap |
|---|---|
| `FULL` | all fields including `secretCode` |
| `MINIMAL` (default) | IDs, category, status, access locations and `secretHash` (lowercase hex SHA-256 of the trimmed, upper-cased secret); names, ranks, plates and secrets are empty |
| `RECEIVE_ONLY` | empty lists (also presence and manifests); terminals call `credential.verify` for every scan |

## Comms engine (messages, alerts, call signalling)

A separate listener on the PC (default port **8444**, same pinned certificate) with its own encrypted database (`xv-comms.db`)
and its own per-terminal key. Terminals learn the port / internet URL with the `comms.info` RPC (device-authenticated, no operator token).

| step | detail |
|---|---|
| key | `commsKey = HMAC-SHA256(deviceKey, "XV-COMMS-1")` |
| clock | `GET /comms/v1/ping` → `serverTime` |
| connect | `wss://host:8444/comms/v1/ws?d={deviceId}&t={serverTime}&n={nonce}&m={hex HMAC-SHA256(commsKey, "XVCM1|hello|d|t|n")}` — 10-minute window, nonce single use |
| frames | text `{"iv","ct"}` AES-256-GCM, AAD `XVCM1|c2s|{d}|{n}|{seq}` (terminal→PC) or `XVCM1|s2c|{d}|{n}|{seq}` (PC→terminal); `seq` counts from 1 per direction and connection |

Plaintext frames:

| `t` | fields | meaning |
|---|---|---|
| `hello` | `serverName`, `serverTime` | first frame from the PC |
| `msg` | `id`, `kind` (`MESSAGE`/`ALERT`), `body` (≤ 2000 chars), `sender`, `ts` | a message; the receiver answers `ack DELIVERED`; duplicates (same `id`) are ignored |
| `ack` | `id`, `state` (`DELIVERED`/`READ`) | delivery / read receipt |
| `ping` / `pong` | – | keep-alive |
| `call` | `op`, `callId`, … | call signalling (below) |

Undelivered PC→terminal messages are sent when the terminal connects; terminals re-send unconfirmed messages on every connection.

### Calls (voice / video)

Signalling uses `call` frames on the Comms connection; media is direct WebRTC (DTLS-SRTP) between the two devices, rendered by the
shared page `shared/call/call.html` (WebView2 on Windows, WebView on Android). No STUN/TURN servers are configured, so only direct
candidates are used: calls work on the same network or over a VPN (e.g. Tailscale), and no media passes through third parties.

| `op` | extra fields | sent by |
|---|---|---|
| `invite` | `video`, `from` | caller |
| `accept` | – | callee, once its call page has camera/microphone ready |
| `decline` / `busy` | – | callee |
| `cancel` | – | caller, before the call was answered (also after 45 s without answer) |
| `offer` / `answer` | `sdp` | caller / callee page |
| `ice` | `candidate` (`candidate`, `sdpMid`, `sdpMLineIndex`) | both pages |
| `hangup` | – | either side |

The PC keeps a call log (direction, video, start / answer / end, outcome) in the Comms database and writes each call to the Audit Trail.

`windows/tests/e2e_protocol_test.py` exercises all of the above.
