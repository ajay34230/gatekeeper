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
| `stations.list` | no | – → locations, gates |
| `auth.login` | no | `username`, `password` |
| `auth.register` | no | `name`, `username` (optional), `password` → `pending` until approved on the PC |
| `auth.logout` | yes | – |
| `master.bootstrap` | yes | – → persons, vehicles, locations, gates, presence |
| `events.create` | yes | person movement event |
| `vehicle.transaction` | yes | `{event, manifest}` vehicle entry/exit with occupants |
| `heartbeat` | yes | `locationId`, `gateId`, `operatorId`, `pending`, `appVersion` |

`windows/tests/e2e_protocol_test.py` exercises all of the above.
