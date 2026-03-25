#!/usr/bin/env python3
import json
from collections import Counter
from pathlib import Path
from datetime import datetime, timezone, timedelta

p = Path('v2rayN/v2rayN/bin/Release/net8.0-windows/metrics/events.jsonl')
if not p.exists():
    p = Path('metrics/events.jsonl')

if not p.exists():
    print('No metrics file found: metrics/events.jsonl')
    raise SystemExit(1)

since = datetime.now(timezone.utc) - timedelta(hours=24)
rows = []
for line in p.read_text(encoding='utf-8', errors='ignore').splitlines():
    line = line.strip()
    if not line:
        continue
    try:
        o = json.loads(line)
    except Exception:
        continue
    try:
        ts = datetime.fromisoformat(o.get('ts', '').replace('Z', '+00:00'))
    except Exception:
        ts = None
    if ts and ts >= since:
        rows.append(o)

c_evt = Counter(r.get('evt') for r in rows)
c_res = Counter((r.get('evt'), r.get('status')) for r in rows)

msg = []
msg.append('VPN metrics (last 24h)')
msg.append(f"events: {len(rows)}")
msg.append(f"connect clicks: {c_evt.get('connect_click',0)}")
msg.append(f"connect ok: {c_res.get(('connect_result','ok'),0)}")
msg.append(f"connect fail: {c_res.get(('connect_result','fail'),0)}")
msg.append(f"connect error: {c_res.get(('connect_result','error'),0)}")
msg.append(f"auto recover ok: {c_res.get(('connect_recover_ok','ok'),0)}")
msg.append(f"auto recover fail: {c_res.get(('connect_recover_fail','fail'),0)}")
msg.append(f"quick fix ok: {c_res.get(('quick_fix_result','ok'),0)}")
msg.append(f"quick fix fail: {c_res.get(('quick_fix_result','fail'),0)}")
print('\n'.join(msg))
