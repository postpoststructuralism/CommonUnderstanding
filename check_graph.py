import json

with open(r'C:\Users\postp\AppData\Local\Temp\map.json') as f:
    d = json.load(f)

print(f'nodes: {len(d["nodes"])}, edges: {len(d["edges"])}')

# Check relationship types
rels = {}
for e in d["edges"]:
    r = e.get("relationship", "unknown")
    rels[r] = rels.get(r, 0) + 1
print(f'Relationship counts: {rels}')