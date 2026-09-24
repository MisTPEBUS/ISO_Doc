# NAS 部署（Synology DS1821+）

單一 instance，Docker Compose 跑 postgres + backend + frontend(nginx)，外層由 Synology DSM 內建
Reverse Proxy 終止 TLS 後轉發進來。三個 service 都不對外露 port（frontend 只綁
`127.0.0.1`），完整信任鏈：DSM(HTTPS) → frontend nginx(HTTP) → backend(HTTP)。

## 前置需求

- NAS 已開 SSH，且 DSM 版本的 Container Manager 支援 `docker compose`（v2 CLI）
- 已建立 GCP bucket `sodu-iso-documents`，並準備具備物件讀取、建立、移動與列出權限的 service account JSON。
- 舊的 `/volume1/ISO` 資料保留；正式環境切換後，API 只從 GCP bucket 讀寫檔案。
- 其餘資料夾（`docker` 共用資料夾底下放 postgres 資料）下面步驟會建立

## 第一次部署

### 1. 把專案送到 NAS

#### 方式一：git clone（建議，之後更新方便）

```bash
git clone <repo-url> /volume1/docker/iso/app
```

#### 方式二：不用 git，直接複製專案資料夾

用 Windows 內建的 SMB 網路磁碟機 + robocopy（先在 DSM 建好 `docker` 共用資料夾）：

```powershell
net use Z: \\<NAS-IP>\docker
robocopy D:\Lobinda\Project\ISO Z:\iso\app /E /XD .git node_modules bin obj storage-data dist .vs .idea
```

或用 [WinSCP](https://winscp.net/) 這類圖形化 SFTP client：連上 NAS（Protocol 選
SFTP/SCP，帳密跟 SSH 一樣），把整個專案資料夾拖到 `/volume1/docker/iso/app`。上傳前
在「Transfer Settings」設排除規則（不然 `node_modules`/`bin`/`obj` 會傳很久，
`storage-data` 更不該把本機開發資料誤傳上去蓋掉 NAS 正式資料）：

```text
|.git/; node_modules/; bin/; obj/; backend\storage-data/; frontend\dist/; .vs/; .idea/
```

傳完後 docker compose 指令還是要下：WinSCP 選單 **Commands → Open Terminal** 可以在
同一個連線直接開終端機，或另開 PuTTY / Windows 內建 `ssh` 連進去執行下面的指令。

也可以整包壓縮後用 scp 傳過去（只有 SSH、沒有 SMB 的情況）：

```bash
tar --exclude=.git --exclude=node_modules --exclude=bin --exclude=obj \
    --exclude=storage-data --exclude=dist -czf iso-app.tar.gz -C D:\Lobinda\Project ISO
scp iso-app.tar.gz <nasuser>@<nas-host>:/volume1/docker/iso/
ssh <nasuser>@<nas-host> "mkdir -p /volume1/docker/iso/app && tar -xzf /volume1/docker/iso/iso-app.tar.gz -C /volume1/docker/iso/app --strip-components=1"
```

> 用方式二的話，之後改版沒有 `git pull` 可用，「之後更新部署」要重複這個複製步驟。

```bash
cd /volume1/docker/iso/app/deploy/nas

# 2. 建立資料目錄與憑證目錄，權限對齊 backend Dockerfile 裡固定的 UID/GID(1654:1654)
mkdir -p /volume1/docker/iso/postgres-data
mkdir -p /volume1/ISO/{store,staging,trash}
chown -R 1654:1654 /volume1/ISO
mkdir -p /volume1/docker/iso/secrets
# 將 service account JSON 放到 /volume1/docker/iso/secrets/gcp-service-account.json，
# 設定容器使用者 1654 可讀；檔案不要放進專案或映像檔。

# 3. 建立正式環境變數檔
cp .env.nas.example .env.nas
# 編輯 .env.nas：設定資料庫密碼、GCP_STORAGE_BUCKET 與 GCP_SERVICE_ACCOUNT_PATH

# 4. 先啟動 postgres，等它 healthy
docker compose -f docker-compose.nas.yml --env-file .env.nas up -d postgres
docker compose -f docker-compose.nas.yml --env-file .env.nas ps
```

### 既有資料

開發端 `storage-data` 不搬遷。正式環境若已有指向地端 `store/` 的 `file_key`，
切換 GCP 前須另行處理這些物件；GCP 儲存供應者只讀取 bucket 中的物件。

### 啟動 backend + frontend

```bash
docker compose -f docker-compose.nas.yml --env-file .env.nas up -d --build backend
docker compose -f docker-compose.nas.yml --env-file .env.nas logs -f backend
# 確認沒有 migration 錯誤、storage health check 有過，再繼續

docker compose -f docker-compose.nas.yml --env-file .env.nas up -d --build frontend
```

### 設定 DSM Reverse Proxy

DSM 控制台 → 登入入口 → 進階 → 反向代理 → 新增規則：
外部 HTTPS（你的網域）→ `127.0.0.1:<NAS_FRONTEND_PORT>`（`.env.nas` 裡設的值，預設 8081）

## 驗證

1. `curl -s https://<你的網域>/api/health` → 200
2. 瀏覽器開 `https://<你的網域>/ISO/`，登入、上傳及下載一份新文件，確認 GCP bucket 內出現 `documents/iso/...` 物件
3. `docker compose -f docker-compose.nas.yml --env-file .env.nas logs backend` 確認沒有 redirect loop / storage 錯誤

## 之後更新部署

**用 git 的話：**

```bash
cd /volume1/docker/iso/app
git pull
cd deploy/nas
docker compose -f docker-compose.nas.yml --env-file .env.nas up -d --build backend frontend
```

**沒用 git 的話：** 重複「第一次部署 → 1. 把專案送到 NAS」的方式二（robocopy 或
tar+scp），把新版檔案覆蓋過去，再跑：

```bash
cd /volume1/docker/iso/app/deploy/nas
docker compose -f docker-compose.nas.yml --env-file .env.nas up -d --build backend frontend
```

## 絕對不要做的事

- 不要把 `postgres`、`backend` 的 port 對外露出（包含 NAS 的 LAN 介面）——ForwardedHeaders
  的信任設定跟 nginx 裡寫死的 `X-Forwarded-Proto: https` 都假設 DSM 是唯一對外入口
- 不要直接複製 `postgres-data` volume 目錄做資料搬遷，版本/檔案系統不保證相容，一律用
  `pg_dump`/`pg_restore`
