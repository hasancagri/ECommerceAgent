#!/usr/bin/env bash
# 086 R4 guard (069'dan devralındı): Elasticsearch arama index'i ile sorgu rehberi (query_storefront tool
# Description) arasındaki ALAN + TİP driftini yakalar. İki kaynak elle tutulan "bilinçli tekrar":
#   - StorefrontSearchIndex.cs: MappingJson (gerçek ES tipleri) + SourceFields (dönüş whitelist'i)
#   - QueryStorefront.cs: tool [Description] prose (LLM'in gördüğü alan+tip listesi)
# Kontroller:
#   A) SourceFields'taki her alan description'da geçer (dönüş alanı belgesiz kalmasın).
#   B) MappingJson'daki her alanın ES tipi, description'da o alanın parantezinde geçer (tip driftini yakalar).
# BİLİNÇLİ SINIR: tek yönlü (description'daki fazladan/bayat satırı ve açıklama metni driftini yakalamaz — review).
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

schema_file="src/services/storefront/Storefront.Api/Search/StorefrontSearchIndex.cs"
prompt_file="src/services/storefront/Storefront.Api/Domains/Storefront/Features/Agents/Queries/QueryStorefront.cs"

for f in "$schema_file" "$prompt_file"; do
    if [ ! -f "$f" ]; then
        echo "check-agent-query-schema: dosya yok: $f"
        exit 1
    fi
done

python3 - "$schema_file" "$prompt_file" <<'PY'
import json, re, sys

schema_file, prompt_file = sys.argv[1], sys.argv[2]
schema = open(schema_file, encoding="utf-8").read()
prompt = open(prompt_file, encoding="utf-8").read()

# SourceFields = [ "a", "b", ... ] bloğundan alan adları.
sf = re.search(r"SourceFields\s*=\s*\[(.*?)\]", schema, re.S)
source_fields = re.findall(r'"([a-z_]+)"', sf.group(1)) if sf else []

# MappingJson = """ { ... } """ bloğunu JSON olarak ayrıştır → alan → ES tipi.
mj = re.search(r'MappingJson\s*=\s*"""(.*?)"""', schema, re.S)
if not mj:
    print("check-agent-query-schema: MappingJson bloğu bulunamadı (format değişti mi?).")
    sys.exit(1)
props = json.loads(mj.group(1))["mappings"]["properties"]
field_types = {k: v.get("type", "object") for k, v in props.items()}

if not source_fields or not field_types:
    print("check-agent-query-schema: alan listesi boş (format değişti mi?).")
    sys.exit(1)

fails = []

def documented(field):
    return re.search(r"\b" + re.escape(field) + r"\b", prompt) is not None

# A) Dönüş whitelist alanları belgeli mi.
for f in source_fields:
    if not documented(f):
        fails.append(f"NAME  '{f}' (SourceFields) tool description'da geçmiyor")

# B) Her mapping alanının ES tipi, alanın parantezinde geçiyor mu ('alan (... tip ...)').
for f, t in sorted(field_types.items()):
    if not documented(f):
        fails.append(f"NAME  '{f}' (mapping) tool description'da geçmiyor")
        continue
    # \balan\s*\( ... \btip\b   — alanın kendi parantezi (effective_price'ın price'ı maskelemesini \b önler)
    pat = r"\b" + re.escape(f) + r"\s*\([^)]*\b" + re.escape(t) + r"\b"
    if not re.search(pat, prompt):
        fails.append(f"TYPE  '{f}' ES tipi '{t}' description'da alanın parantezinde eşleşmiyor")

if fails:
    for line in fails:
        print("DRIFT " + line)
    print("check-agent-query-schema: DRIFT var — ES index alan/tip ↔ tool description hizasız.")
    sys.exit(1)

print(f"check-agent-query-schema: OK — {len(source_fields)} dönüş alanı + {len(field_types)} mapping tipi, hepsi tool description'da hizalı.")
PY