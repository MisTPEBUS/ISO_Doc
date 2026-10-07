# 雲端部署

Docker Compose 只跑 `iso-frontend`（nginx）與 `iso-backend`（API）。資料庫使用 GCP Postgres，
檔案存放於 GCP bucket，不在這台主機上。

```text
使用者 ─HTTPS→ reverse proxy（終止 TLS）─HTTP→ iso-frontend:80
                                              ├─ /ISO/  前端靜態檔
                                              └─ /api/  → iso-backend:8080 ─→ GCP Postgres / GCP bucket
```

- `iso-backend` 不開 port，也不接 `backend_network`，只有 `iso-frontend` 連得到。
- `iso-frontend` 只綁 `127.0.0.1`，並加入既有的 `backend_network`，讓 reverse proxy 可以用
  `127.0.0.1:${ISO_FRONTEND_PORT}` 或 `iso-frontend:80` 轉發。
- 前端與 API 同網域（nginx 轉發 `/api/`），所以後端不需要 CORS 設定。

稽核紀錄的 `ip` 必須是使用者 IP：在 `.env.deploy` 將 `TRUSTED_UPSTREAM_PROXY` 設為反向代理連到 `iso-frontend` 時的**來源 IP 或最小 CIDR**。若代理走主機的 `127.0.0.1:${ISO_FRONTEND_PORT}`，此值是 frontend container 看到的 Docker gateway IP；若走 `backend_network`，此值是反向代理容器的 IP／所屬小網段。前方代理必須將實際使用者 IP 附加到 `X-Forwarded-For`；frontend Nginx 只在來源符合設定時才解析該標頭，並將已驗證的使用者 IP 傳給後端。不得填 `0.0.0.0/0`。未設定此變數時 Compose 不會啟動 frontend，避免將代理 IP 當成使用者 IP。

部署後從外部網路下載一份主文，檢查 `audit_logs` 最新的 `DOWNLOAD_DOCUMENT` 列：`ip` 應是該使用者的公網 IP，不應是 frontend／反向代理容器或 Docker gateway 的 IP；同時可用 `detail->>'download_id'` 對照 PDF 浮水印。

## 前置需求

- 主機已安裝 Docker 與 `docker compose`（v2 CLI）。
- 已存在 `backend_network`；沒有的話先建立：`docker network create backend_network`。
- GCP Postgres 已建立資料庫，且允許這台主機連線。
- service account JSON 放在主機上（不要放進專案目錄），需具備 bucket 物件的讀取、建立、移動與列出權限。
  container 以 uid `1654` 執行，檔案要讓它讀得到，例如 `chmod 644`。

## `.env.deploy` 參數

複製 `.env.deploy.example` 後填入下列值。前四項不可留空；Compose 會在啟動前檢查。

| 參數 | 值 |
| --- | --- |
| `ISO_DB_CONNECTION` | GCP PostgreSQL 的 Npgsql 連線字串，包含 `Host`、`Port`、`Database`、`Username`、`Password`；請填實際資料庫資訊。 |
| `GCP_STORAGE_BUCKET` | ISO 文件使用的 GCS bucket 名稱；目前範本為 `sodu-iso-documents`，請確認雲端環境是否使用此 bucket。 |
| `GCP_SERVICE_ACCOUNT_PATH` | 主機上的 service account JSON **絕對路徑**，例如 `/opt/iso/secrets/gcp-service-account.json`。檔案必須已存在，且 container uid `1654` 可讀。 |
| `TRUSTED_UPSTREAM_PROXY` | 反向代理連到 `iso-frontend` 時的來源 IP 或最小 CIDR；依實際網路配置填寫，不可用 `0.0.0.0/0`。 |
| `ISO_FRONTEND_PORT` | 主機 loopback port，預設 `8081`；既有 `linehook-api` 使用 `5001`，兩者不衝突。 |
| `OPENAI_API_KEY` | AI 匯入功能使用；不用該功能可留空。 |

既有 `example.deploy.yml` 的 `linehook-api` 使用 `backend_network`，可與本專案共用該網路；它的 `ASPNETCORE_CORS_ORIGINS` 是該服務自己的設定，本專案的前端和 API 走同一網域，不需複製。若反向代理以同一主機名稱提供兩個服務，須讓 `/ISO/` 與 `/api/` 轉到 `iso-frontend`；若 `/api/` 已由其他服務使用，請為 ISO 配置獨立主機名稱。

## 部署

```bash
git clone <repo-url> iso
cd iso/deploy/cloud
cp .env.deploy.example .env.deploy   # 填入實際值
docker compose -f docker-compose-deploy.yml --env-file .env.deploy up -d --build
docker compose -f docker-compose-deploy.yml --env-file .env.deploy logs -f iso-backend
```

`iso-backend` 啟動時會自動套用 migration，並檢查 GCP bucket 是否可用；檢查失敗時 container 會直接結束，
`iso-frontend` 也不會啟動。

新資料庫會自動建立 `admin` / `admin`（`SYSTEM_ADMIN`），**第一次登入後請立即修改密碼**。

## 更新

```bash
cd iso && git pull
cd deploy/cloud
docker compose -f docker-compose-deploy.yml --env-file .env.deploy up -d --build
```

`pull_policy: if_not_present` 不會自動重建 image，更新時一定要加 `--build`。

## Reverse proxy 設定重點

- `/ISO/` 與 `/api/` 兩個路徑都要轉到 `iso-frontend`，且必須走 HTTPS。
  nginx 固定送出 `X-Forwarded-Proto https`，登入 cookie 會依此加上 `Secure`，以 HTTP 連線將無法登入。
- 若同一個網域的 `/api/` 已經給其他服務使用，請改用獨立子網域，例如 `iso.<ip>.nip.io`。
- 上傳檔案上限為 50 MB（`frontend/nginx.conf`），reverse proxy 的上限不可小於此值。
