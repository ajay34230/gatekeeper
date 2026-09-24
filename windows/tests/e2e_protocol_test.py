"""End-to-end protocol test: behaves exactly like the Android terminal (pinning, pairing, AES-GCM envelope)."""
import base64, hashlib, json, os, ssl, subprocess, sys, time, uuid, socket
import requests
from Crypto.Cipher import AES

qr, host = sys.argv[1], "https://127.0.0.1:8443"
cfg = json.loads(base64.urlsafe_b64decode(qr.split(":", 1)[1] + "=" * (-len(qr.split(":", 1)[1]) % 4)))

# 1. certificate pinning: the server certificate must hash to the fingerprint in the QR
der = ssl.PEM_cert_to_DER_cert(ssl.get_server_certificate(("127.0.0.1", cfg["p"])))
assert hashlib.sha256(der).hexdigest() == cfg["fp"], "pin mismatch"
S = requests.Session(); S.trust_env = False; S.verify = False
requests.packages.urllib3.disable_warnings()

print("ping", S.get(host + "/api/v1/ping").json()["serverId"])
enr = S.post(host + "/api/v1/pair/enroll", json={"code": cfg["c"], "deviceName": "Test", "model": "pytest"}).json()
dev, key = enr["deviceId"], base64.b64decode(enr["deviceKey"])
assert S.post(host + "/api/v1/pair/enroll", json={"code": cfg["c"]}).status_code == 403, "pair code must be single use"

def rpc(op, data=None, token=None, replay=None):
    ts, nonce = int(time.time() * 1000), base64.urlsafe_b64encode(os.urandom(16)).decode().rstrip("=")
    if replay: ts, nonce = replay
    iv = os.urandom(12); c = AES.new(key, AES.MODE_GCM, nonce=iv)
    c.update(f"XVGK1|req|{dev}|{ts}|{nonce}".encode())
    ct, tag = c.encrypt_and_digest(json.dumps({"op": op, "data": data or {}, "token": token}).encode())
    r = S.post(host + "/api/v1/rpc", headers={"X-GK-Device": dev, "X-GK-Ts": str(ts), "X-GK-Nonce": nonce},
               json={"iv": base64.b64encode(iv).decode(), "ct": base64.b64encode(ct + tag).decode()})
    if r.status_code != 200: return r.status_code, r.json(), (ts, nonce)
    env = r.json(); raw = base64.b64decode(env["ct"])
    d = AES.new(key, AES.MODE_GCM, nonce=base64.b64decode(env["iv"])); d.update(f"XVGK1|res|{dev}|{nonce}".encode())
    out = json.loads(d.decrypt_and_verify(raw[:-16], raw[-16:]))
    return out["code"], out["body"], (ts, nonce)

code, body, used = rpc("health"); assert code == 200, body
assert rpc("health", replay=used)[0] == 401, "replay must be rejected"
assert rpc("master.bootstrap")[0] == 401, "data requires operator login"
code, body, _ = rpc("auth.register", {"name": "Test Recruit", "password": "abcdef1"}); assert body["status"] == "pending", body
code, body, _ = rpc("auth.login", {"username": body["username"], "password": "abcdef1"}); assert code == 403, (code, body)
code, login, _ = rpc("auth.login", {"username": "GK-01", "password": "Operator#1"}); assert code == 200, login
tok = login["accessToken"]
code, boot, _ = rpc("master.bootstrap", token=tok); assert code == 200
print("bootstrap persons", len(boot["persons"]), "vehicles", len(boot["vehicles"]))
def ev(eid, etype, ent, typ, extra=None):
    now = int(time.time() * 1000)
    e = {"eventId": eid, "entityType": etype, "entityId": ent, "eventType": typ, "locationId": "LOC07", "gateId": "G02",
         "deviceId": dev, "operatorId": "GK-01", "eventTimestamp": now, "createdAt": now, "sourceType": "DIRECT"}
    e.update(extra or {}); return e
e1 = ev("EVT-1", "PERSON", "P001", "ENTRY")
print("entry", rpc("events.create", e1, tok)[:2])
print("dup  ", rpc("events.create", e1, tok)[:2])
print("again", rpc("events.create", ev("EVT-2", "PERSON", "P001", "ENTRY"), tok)[:2])
print("exit ", rpc("events.create", ev("EVT-3", "PERSON", "P001", "EXIT", {"locationMismatch": True, "scannedLocation": "LOC04"}), tok)[:2])
ve = ev("EVT-4", "VEHICLE", "V014", "ENTRY")
man = {"manifestId": "MNF-1", "vehicleId": "V014", "entryEventId": "EVT-4", "locationId": "LOC07", "gateId": "G02", "driverId": "P001",
       "coDriverId": None, "occupants": ["P001"], "createdAt": ve["createdAt"], "state": "ACTIVE"}
print("veh in ", rpc("vehicle.transaction", {"event": ve, "manifest": man}, tok)[:2])
vx = ev("EVT-5", "VEHICLE", "V014", "EXIT"); man2 = dict(man, state="EXITED", exitEventId="EVT-5", exitAt=vx["eventTimestamp"])
print("veh out", rpc("vehicle.transaction", {"event": vx, "manifest": man2}, tok)[:2])
print("hb", rpc("heartbeat", {"locationId": "LOC07", "gateId": "G02", "operatorId": "GK-01", "pending": 0, "appVersion": "t"}, tok)[:2])
# discovery
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); s.settimeout(3); s.sendto(b"XVGK_DISCOVER_V1", ("127.0.0.1", 47913))
print("discovery", json.loads(s.recvfrom(2048)[0])["serverId"])
print("ALL CHECKS PASSED")
