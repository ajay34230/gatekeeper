"""Cloud Link test: a terminal that reaches the Command Center ONLY through its internet address (a tunnel with a
public certificate, e.g. a Cloudflare quick tunnel). Pairs, signs in, syncs and records entry/exit through the tunnel;
every request stays AES-256-GCM encrypted end to end."""
import base64, json, os, sys, time
import requests
from Crypto.Cipher import AES

qr = sys.argv[1]
b = qr.split(":", 1)[1]
cfg = json.loads(base64.urlsafe_b64decode(b + "=" * (-len(b) % 4)))
url = cfg["u"].rstrip("/")
assert url.startswith("https://") and cfg["pc"], ("pairing QR carries no public Cloud Link address", cfg.get("u"), cfg.get("pc"))
S = requests.Session()  # normal certificate validation: the tunnel presents a public certificate

for _ in range(30):  # a new tunnel can take a few seconds to route
    try:
        if S.get(url + "/api/v1/ping", timeout=10).json()["serverId"] == cfg["id"]: break
    except Exception: pass
    time.sleep(2)
else: sys.exit("Cloud Link address never answered")

enr = S.post(url + "/api/v1/pair/enroll", json={"code": cfg["c"], "deviceName": "Cloud test", "model": "pytest"}, timeout=20).json()
dev, key = enr["deviceId"], base64.b64decode(enr["deviceKey"])

def rpc(op, data=None, token=None):
    ts, nonce = int(time.time() * 1000), base64.urlsafe_b64encode(os.urandom(16)).decode().rstrip("=")
    iv = os.urandom(12); c = AES.new(key, AES.MODE_GCM, nonce=iv); c.update(f"XVGK1|req|{dev}|{ts}|{nonce}".encode())
    ct, tag = c.encrypt_and_digest(json.dumps({"op": op, "data": data or {}, "token": token}).encode())
    r = S.post(url + "/api/v1/rpc", headers={"X-GK-Device": dev, "X-GK-Ts": str(ts), "X-GK-Nonce": nonce},
               json={"iv": base64.b64encode(iv).decode(), "ct": base64.b64encode(ct + tag).decode()}, timeout=20)
    assert r.status_code == 200, (op, r.status_code, r.text[:200])
    env = r.json(); raw = base64.b64decode(env["ct"])
    d = AES.new(key, AES.MODE_GCM, nonce=base64.b64decode(env["iv"])); d.update(f"XVGK1|res|{dev}|{nonce}".encode())
    out = json.loads(d.decrypt_and_verify(raw[:-16], raw[-16:]))
    return out["code"], out["body"]

code, h = rpc("health"); assert code == 200, h
code, login = rpc("auth.login", {"username": "GK-01", "password": "Operator#1"}); assert code == 200, login
tok = login["accessToken"]
code, boot = rpc("master.bootstrap", token=tok); assert code == 200 and boot["persons"], boot
code, info = rpc("comms.info"); assert code == 200 and info["publicUrl"].startswith("https://"), info
code, who = rpc("credential.verify", {"code": "CI-0001", "expected": "PERSON"}, tok); assert code == 200 and who["id"] == "P001", who
now = int(time.time() * 1000)
def ev(eid, typ):
    return {"eventId": eid, "entityType": "PERSON", "entityId": "P001", "eventType": typ, "locationId": "LOC07", "gateId": "G02",
            "deviceId": dev, "operatorId": "GK-01", "eventTimestamp": int(time.time() * 1000), "createdAt": now, "sourceType": "DIRECT"}
code, body = rpc("events.create", ev("CLOUD-1", "ENTRY"), tok); assert code == 201, body
code, body = rpc("events.create", ev("CLOUD-2", "EXIT"), tok); assert code == 201, body
print("CLOUD LINK OK via", url.split("//")[1].split(".")[0] + ".<tunnel>")
