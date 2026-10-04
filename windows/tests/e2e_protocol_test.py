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

def rpc(op, data=None, token=None, replay=None, skew=0):
    ts, nonce = int(time.time() * 1000) + skew, base64.urlsafe_b64encode(os.urandom(16)).decode().rstrip("=")
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
# default Data Sharing mode is MINIMAL: no names or secrets on terminals, only IDs, status and secret hashes
assert boot["sharingMode"] == "MINIMAL", boot["sharingMode"]
assert boot["persons"] and all(p["name"] == "" and p["secretCode"] == "" and len(p["secretHash"]) == 64 for p in boot["persons"]), boot["persons"][:1]
assert all(v["registration"] == "" and v["secretCode"] == "" for v in boot["vehicles"])
# online badge check returns the details for that one scan only
code, who, _ = rpc("credential.verify", {"code": "CI-0001", "expected": "PERSON"}, tok); assert code == 200 and who["name"] == "CI Test Soldier", (code, who)
code, who, _ = rpc("credential.verify", {"code": "NO-SUCH-BADGE", "expected": ""}, tok); assert code == 404, (code, who)
assert rpc("credential.verify", {"code": "CI-0001"})[0] == 401, "verify requires operator login"
def ev(eid, etype, ent, typ, extra=None):
    now = int(time.time() * 1000)
    e = {"eventId": eid, "entityType": etype, "entityId": ent, "eventType": typ, "locationId": "LOC07", "gateId": "G02",
         "deviceId": dev, "operatorId": "GK-01", "eventTimestamp": now, "createdAt": now, "sourceType": "DIRECT"}
    e.update(extra or {}); return e
code, st, _ = rpc("stations.list"); assert code == 200 and "Proceeding on Leave" in st["reasons"], st
assert "TD" in boot["reasons"], boot.get("reasons")
e1 = ev("EVT-1", "PERSON", "P001", "ENTRY", {"reason": "Rejoining from Leave", "remarks": "Pass no. 42"})
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
# clock skew: rejected with a reason the phone uses to resynchronize from /ping
code, body, _ = rpc("health", skew=3_600_000); assert code == 401 and body["reason"] == "STALE_OR_REPLAYED", (code, body)
assert abs(S.get(host + "/api/v1/ping").json()["serverTime"] - time.time() * 1000) < 60_000
# record captured offline by another existing operator on this terminal is accepted
e6 = ev("EVT-6", "PERSON", "P001", "ENTRY"); e6["operatorId"] = "GK-02"
assert rpc("events.create", e6, tok)[0] == 403, "unknown operator must be rejected"
code, other, _ = rpc("auth.register", {"name": "Second Operator", "username": "GK-03", "password": "abcdef1"})
e6["operatorId"] = "GK-03"; code, body, _ = rpc("events.create", e6, tok); assert code == 201, (code, body)
# person who arrived in a vehicle leaves on foot
vi = ev("EVT-7", "VEHICLE", "V014", "ENTRY"); m3 = dict(man, manifestId="MNF-2", entryEventId="EVT-7", occupants=["P002"], driverId="P002", createdAt=vi["createdAt"])
code, body, _ = rpc("vehicle.transaction", {"event": vi, "manifest": m3}, tok); assert code == 201, (code, body)
code, body, _ = rpc("events.create", ev("EVT-8", "PERSON", "P002", "EXIT", {"sourceType": "VEHICLE", "sourceId": "MNF-2"}), tok); assert code == 201, (code, body)
code, boot2, _ = rpc("master.bootstrap", token=tok); assert any(m["manifestId"] == "MNF-2" for m in boot2["manifests"]), boot2["manifests"]
print("hb", rpc("heartbeat", {"locationId": "LOC07", "gateId": "G02", "operatorId": "GK-01", "pending": 0, "appVersion": "t"}, tok)[:2])
# discovery
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM); s.settimeout(3); s.sendto(b"XVGK_DISCOVER_V1", ("127.0.0.1", 47913))
print("discovery", json.loads(s.recvfrom(2048)[0])["serverId"])
# ---- Comms engine: separate TLS listener, key derived from the device key, every frame AES-GCM sealed
import hmac, websocket
code, info, _ = rpc("comms.info"); assert code == 200 and info["port"] == 8444, (code, info)
der2 = ssl.PEM_cert_to_DER_cert(ssl.get_server_certificate(("127.0.0.1", info["port"])))
assert hashlib.sha256(der2).hexdigest() == cfg["fp"], "comms engine must present the pinned certificate"
ckey = hmac.new(key, b"XV-COMMS-1", hashlib.sha256).digest()
def comms_connect(mac_key=ckey):
    ts, n = int(time.time() * 1000), base64.urlsafe_b64encode(os.urandom(16)).decode().rstrip("=")
    mac = hmac.new(mac_key, f"XVCM1|hello|{dev}|{ts}|{n}".encode(), hashlib.sha256).hexdigest()
    ws = websocket.create_connection(f"wss://127.0.0.1:{info['port']}/comms/v1/ws?d={dev}&t={ts}&n={n}&m={mac}", sslopt={"cert_reqs": ssl.CERT_NONE}, timeout=10)
    return ws, n
try: comms_connect(os.urandom(32)); raise SystemExit("comms must reject a wrong key")
except websocket.WebSocketBadStatusException as e: assert e.status_code == 401
ws, cn = comms_connect(); seq = {"in": 0, "out": 0}
def csend(obj):
    seq["out"] += 1; iv = os.urandom(12); c = AES.new(ckey, AES.MODE_GCM, nonce=iv); c.update(f"XVCM1|c2s|{dev}|{cn}|{seq['out']}".encode())
    ct, tag = c.encrypt_and_digest(json.dumps(obj).encode()); ws.send(json.dumps({"iv": base64.b64encode(iv).decode(), "ct": base64.b64encode(ct + tag).decode()}))
def crecv():
    env = json.loads(ws.recv()); raw = base64.b64decode(env["ct"]); seq["in"] += 1
    d = AES.new(ckey, AES.MODE_GCM, nonce=base64.b64decode(env["iv"])); d.update(f"XVCM1|s2c|{dev}|{cn}|{seq['in']}".encode())
    return json.loads(d.decrypt_and_verify(raw[:-16], raw[-16:]))
assert crecv()["t"] == "hello"
mid = uuid.uuid4().hex
csend({"t": "msg", "id": mid, "kind": "ALERT", "body": "Test alert from gate", "sender": "GK-01"})
got = [crecv(), crecv()]
assert {"t": "ack", "id": mid, "state": "DELIVERED"} in got, got
echo = next(f for f in got if f["t"] == "msg"); assert echo["body"] == "Echo: Test alert from gate", echo
csend({"t": "ack", "id": echo["id"], "state": "READ"})
csend({"t": "msg", "id": mid, "kind": "ALERT", "body": "Test alert from gate"})  # resend is idempotent: ack only, no second echo
assert crecv() == {"t": "ack", "id": mid, "state": "DELIVERED"}
csend({"t": "ping"}); assert crecv()["t"] == "pong"
csend({"t": "call", "op": "invite", "callId": "c" * 32, "video": True, "from": "GK-01"})
assert crecv() == {"t": "call", "op": "busy", "callId": "c" * 32}, "call signalling must round-trip through the Comms engine"
ws.close()
print("comms ok")
# ---- visitor passes: entry only inside the validity window
log = open(os.environ.get("XV_SRV_LOG", "srv.log")).read()
vis = [l.split()[1] for l in log.splitlines() if l.startswith("VISITOR ")]
assert len(vis) == 2, vis
code, body, _ = rpc("events.create", ev("EVT-V1", "PERSON", vis[0], "ENTRY"), tok); assert code == 201, (code, body)
code, body, _ = rpc("events.create", ev("EVT-V2", "PERSON", vis[1], "ENTRY"), tok); assert code == 403 and body["reason"] == "PASS_EXPIRED", (code, body)
code, boot3, _ = rpc("master.bootstrap", token=tok)
vp = {p["personId"]: p for p in boot3["persons"]}
assert vp[vis[0]]["validTo"] > time.time() * 1000 > vp[vis[0]]["validFrom"], vp[vis[0]]
code, body, _ = rpc("events.create", ev("EVT-V3", "PERSON", vis[0], "EXIT"), tok); assert code == 201, (code, body)
# a visitor whose pass expired cannot enter on board a vehicle either
vx2 = ev("EVT-V4", "VEHICLE", "V014", "EXIT"); m4 = dict(m3, state="EXITED", exitEventId="EVT-V4", exitAt=vx2["eventTimestamp"])
code, body, _ = rpc("vehicle.transaction", {"event": vx2, "manifest": m4}, tok); assert code == 201, (code, body)
vi2 = ev("EVT-V5", "VEHICLE", "V014", "ENTRY")
m5 = dict(man, manifestId="MNF-V5", entryEventId="EVT-V5", occupants=[vis[1]], driverId=vis[1], createdAt=vi2["createdAt"])
code, body, _ = rpc("vehicle.transaction", {"event": vi2, "manifest": m5}, tok); assert code == 403 and body["reason"] == "PASS_EXPIRED", (code, body)
print("visitor passes ok")
# ---- failure cases the gate must handle safely
code, body, _ = rpc("auth.login", {"username": "GK-01", "password": "wrong-password"}); assert code == 401 and body["reason"] == "INVALID_CREDENTIALS", (code, body)
for _ in range(5): rpc("auth.login", {"username": "GK-77", "password": "nope"})
code, body, _ = rpc("auth.login", {"username": "GK-77", "password": "nope"}); assert code == 429 and body["reason"] == "TOO_MANY_ATTEMPTS", (code, body)
ts, nonce = int(time.time() * 1000), base64.urlsafe_b64encode(os.urandom(16)).decode().rstrip("=")
r = S.post(host + "/api/v1/rpc", headers={"X-GK-Device": dev, "X-GK-Ts": str(ts), "X-GK-Nonce": nonce}, json={"iv": base64.b64encode(os.urandom(12)).decode(), "ct": base64.b64encode(os.urandom(48)).decode()})
assert r.status_code == 400 and r.json()["reason"] == "DECRYPT_FAILED", (r.status_code, r.text)
r = S.post(host + "/api/v1/rpc", headers={"X-GK-Device": dev, "X-GK-Ts": str(ts), "X-GK-Nonce": nonce + "x"}, data="not json")
assert r.status_code == 400, r.status_code
code, body, _ = rpc("events.create", dict(ev("EVT-1", "PERSON", "P001", "EXIT")), tok); assert code == 409 and body["reason"] == "EVENT_ID_REUSED", (code, body)
code, body, _ = rpc("events.create", ev("EVT-F1", "PERSON", "P002", "EXIT"), tok); assert code == 409 and body["reason"] == "NOT_INSIDE", (code, body)
code, body, _ = rpc("events.create", {"eventId": "EVT-F2"}, tok); assert code == 400, (code, body)
code, body, _ = rpc("no.such.operation", {}, tok); assert code == 404, (code, body)
# two gates scan the same person at the same moment: exactly one entry is recorded
import threading
res = []
def gate(i): res.append(rpc("events.create", dict(ev(f"EVT-RACE-{i}", "PERSON", "P002", "ENTRY"), gateId=f"G0{i}"), tok)[0])
th = [threading.Thread(target=gate, args=(i,)) for i in range(1, 5)]; [t.start() for t in th]; [t.join() for t in th]
assert sorted(res) == [201, 409, 409, 409], res
code, body, _ = rpc("events.create", ev("EVT-RACE-X", "PERSON", "P002", "EXIT"), tok); assert code == 201, (code, body)
# a suspended person is refused at the gate (entry) and shown as suspended on verify
open(os.path.join(os.environ["XV_CI_DATA"], "ci-suspend"), "w").write("P002")
for _ in range(50):
    if "SUSPENDED P002" in open(os.environ.get("XV_SRV_LOG", "srv.log")).read(): break
    time.sleep(0.2)
code, body, _ = rpc("events.create", ev("EVT-S1", "PERSON", "P002", "ENTRY"), tok); assert code == 409 and body["reason"] == "INACTIVE_PERSON", (code, body)
code, who, _ = rpc("credential.verify", {"code": "CI-0002", "expected": "PERSON"}, tok); assert code == 200 and who["status"] == "SUSPENDED", (code, who)
print("failure cases ok")
# ---- leave tracking: exit with reason + expected return date (already passed → overdue on the PC)
code, st2, _ = rpc("stations.list"); assert "TD" in st2["returnReasons"], st2
code, body, _ = rpc("events.create", ev("EVT-L1", "PERSON", "P001", "EXIT", {"reason": "TD", "remarks": "CI", "expectedReturn": int(time.time() * 1000) - 3_600_000}), tok)
assert code == 201, (code, body)
print("leave ok")

# ---- vehicle transit: destination on exit, alerts for the destination RP, nothing is ever assumed reached
H = 3_600_000
now0 = int(time.time() * 1000)
def vtx(eid, typ, mid, ts, extra=None, loc="LOC07"):
    e = ev(eid, "VEHICLE", "V014", typ, dict({"eventTimestamp": ts, "createdAt": ts, "locationId": loc}, **(extra or {})))
    m = {"manifestId": mid, "vehicleId": "V014", "entryEventId": eid, "locationId": loc, "gateId": "G02", "driverId": "P001",
         "coDriverId": None, "occupants": ["P001"], "createdAt": ts, "state": "ACTIVE"}
    if typ == "EXIT": m = dict(m, state="EXITED", exitEventId=eid, exitAt=ts)
    return rpc("vehicle.transaction", {"event": e, "manifest": m}, tok)
assert vtx("EVT-T1", "ENTRY", "MNF-T1", now0 - 3 * H)[0] == 201
assert vtx("EVT-T2", "EXIT", "MNF-T1", now0 - 2 * H, {"destinationId": "LOC08", "transitMinutes": 30})[0] == 201
rpc("heartbeat", {"locationId": "LOC08", "gateId": "G02", "operatorId": "GK-01", "pending": 0, "appVersion": "ci"}, tok)
code, op, _ = rpc("transit.open", {}, tok)
assert code == 200 and len(op["trips"]) == 1, (code, op)
trip = op["trips"][0]
assert trip["overdue"] is True and trip["expectedMin"] == 30 and trip["destId"] == "LOC08" and trip["fromId"] == "LOC07", trip
tid = trip["transitId"]
assert rpc("transit.snooze", {"transitId": tid}, tok)[0] == 200
code, body, _ = rpc("transit.resolve", {"transitId": tid, "kind": "REACHED", "minutes": 0}, tok); assert code == 400 and body["reason"] == "INVALID_MINUTES", (code, body)
code, body, _ = rpc("transit.resolve", {"transitId": tid, "kind": "DIVERTED", "minutes": 50}, tok); assert code == 400 and body["reason"] == "PLACE_REQUIRED", (code, body)
code, body, _ = rpc("transit.resolve", {"transitId": tid, "kind": "REACHED", "minutes": 300}, tok); assert code == 400 and body["reason"] == "INVALID_MINUTES", (code, body)
assert rpc("transit.open", {}, tok)[1]["trips"][0]["overdue"] is True, "still not reached: nothing is assumed"
code, body, _ = rpc("transit.resolve", {"transitId": tid, "kind": "REACHED", "minutes": 45}, tok); assert code == 200, (code, body)
assert rpc("transit.open", {}, tok)[1]["trips"] == []
# unknown destination: only the server can close it; a gate scan elsewhere closes it as DIVERTED
assert vtx("EVT-T3", "ENTRY", "MNF-T2", now0 - 100 * 60_000)[0] == 201
assert vtx("EVT-T4", "EXIT", "MNF-T2", now0 - 90 * 60_000, {"destinationName": "Ammo Depot"})[0] == 201
assert rpc("transit.open", {}, tok)[1]["trips"] == [], "a trip to an unknown place is not shown to any RP"
assert vtx("EVT-T5", "ENTRY", "MNF-T3", now0 - 60 * 60_000, loc="LOC08")[0] == 201
# scan at the planned destination closes the trip as REACHED
assert vtx("EVT-T6", "EXIT", "MNF-T3", now0 - 30 * 60_000, {"destinationId": "LOC07", "transitMinutes": 20}, loc="LOC08")[0] == 201
assert vtx("EVT-T7", "ENTRY", "MNF-T4", now0 - 5 * 60_000)[0] == 201
print("transit ok")

# ---- security hardening
# forged requests (wrong key) are refused and do not consume nonces of the real terminal
ts, n = int(time.time() * 1000), base64.urlsafe_b64encode(os.urandom(16)).decode().rstrip("=")
iv = os.urandom(12); c = AES.new(os.urandom(32), AES.MODE_GCM, nonce=iv); c.update(f"XVGK1|req|{dev}|{ts}|{n}".encode())
ct, tag = c.encrypt_and_digest(b'{"op":"health","data":{}}')
r = S.post(host + "/api/v1/rpc", headers={"X-GK-Device": dev, "X-GK-Ts": str(ts), "X-GK-Nonce": n}, json={"iv": base64.b64encode(iv).decode(), "ct": base64.b64encode(ct + tag).decode()})
assert r.status_code == 400, r.status_code
assert rpc("health", replay=(ts, n))[0] == 200, "a nonce used by a forged request must still be usable by the real terminal"
# pairing is rate limited per source address
codes = [S.post(host + "/api/v1/pair/enroll", json={"code": "WRONG" + str(i)}).status_code for i in range(12)]
assert 429 in codes, codes
print("security ok")
# ---- remote wipe: a revoked terminal is told to erase its data
ci = os.environ.get("XV_CI_DATA")
if ci:
    open(os.path.join(ci, "ci-revoke"), "w").write(dev)
    for _ in range(40):
        time.sleep(0.25)
        code, body, _ = rpc("health")
        if code == 401: break
    assert code == 401 and body.get("reason") == "DEVICE_REVOKED", (code, body)
    print("remote wipe order ok")
print("ALL CHECKS PASSED")
