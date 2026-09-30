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

## 前置需求

- 主機已安裝 Docker 與 `docker compose`（v2 CLI）。
- 已存在 `backend_network`；沒有的話先建立：`docker network create backend_network`。
- GCP Postgres 已建立資料庫，且允許這台主機連線。
- service account JSON 放在主機上（不要放進專案目錄），需具備 bucket 物件的讀取、建立、移動與列出權限。
  container 以 uid `1654` 執行，檔案要讓它讀得到，例如 `chmod 644`。

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
