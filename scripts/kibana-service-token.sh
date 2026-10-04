#!/usr/bin/env bash
# Kibana (dev) için Elasticsearch service-account token üretir ve AppHost user-secrets'a yazar.
# Kibana 8.x elastic superuser'ı reddeder → kibana_system service token şart. Token ES data
# volume'de kalıcı; yalnız tam reset (volume silme) sonrası yeniden çalıştır.
#
# Kullanım: AppHost ayaktayken (ES Running) çalıştır:  scripts/kibana-service-token.sh
set -euo pipefail

ES_CONTAINER=$(docker ps --format '{{.Names}}' | grep -i '^elasticsearch' | head -1)
[ -z "$ES_CONTAINER" ] && { echo "HATA: çalışan elasticsearch container yok (AppHost ayakta mı?)"; exit 1; }

PORT=$(docker port "$ES_CONTAINER" 9200/tcp | head -1 | awk -F: '{print $2}')
PW=$(docker inspect "$ES_CONTAINER" --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -i ELASTIC_PASSWORD | cut -d= -f2)

# Aynı adlı token varsa önce sil (idempotent), sonra üret.
curl -s -u "elastic:$PW" -XDELETE "http://127.0.0.1:$PORT/_security/service/elastic/kibana/credential/token/aspire-kibana" >/dev/null || true
TOKEN=$(curl -s -u "elastic:$PW" -XPOST "http://127.0.0.1:$PORT/_security/service/elastic/kibana/credential/token/aspire-kibana" \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['token']['value'])")

APPHOST="$(cd "$(dirname "$0")/.." && pwd)/src/aspire/AppHost/AppHost.csproj"
dotnet user-secrets set "Parameters:kibana-service-token" "$TOKEN" --project "$APPHOST"
echo "OK: token user-secrets'a yazıldı. Kibana kaynağını (yeniden) başlat."