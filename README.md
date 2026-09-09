# PostgreSQL 16 Docker

本專案提供 development、test、production 三個獨立的 PostgreSQL 16 容器。每個環境都有自己的資料庫、連接埠及持久化 Docker volume。

## 啟動全部環境

```powershell
docker compose up -d --build
```

只啟動單一環境：

```powershell
docker compose up -d development
docker compose up -d test
docker compose up -d production
```

## 連線參數

### Development

```yaml
host: localhost
database: iso_development
port: 6515
username: root
password: admin
```

連線字串：

```text
postgresql://root:admin@localhost:6515/iso_development
```

### Test

```yaml
host: localhost
database: iso_test
port: 2408
username: root
password:
```

test 使用 `POSTGRES_HOST_AUTH_METHOD=trust`，僅建議用於本機測試。

連線字串：

```text
postgresql://root@localhost:2408/iso_test
```

### Production

```yaml
host: localhost
database: iso_production
port: 2303
username: root
password: admin
```

連線字串：

```text
postgresql://root:admin@localhost:2303/iso_production
```

## 管理指令

查看容器狀態：

```powershell
docker compose ps
```

進入 development 資料庫：

```powershell
docker exec -it iso-postgres-development psql -U root -d iso_development
```

停止容器並保留資料：

```powershell
docker compose down
```

刪除容器及所有資料 volume（資料無法復原）：

```powershell
docker compose down -v
```

### Legacy（舊系統 MySQL 5.7，資料遷移參考用）

```yaml
host: localhost
database: iso_production
port: 3307
username: root
password: admin
```

啟動並還原舊資料：

```powershell
docker compose up -d legacy
docker exec -i iso-mysql-legacy sh -c 'exec mysql -uroot -p"$MYSQL_ROOT_PASSWORD" iso_production' < D:\qnap-ubuntu-export\iso_production.sql
```

舊 dump 檔（含正式資料）不進版控，需另外取得。

## 資料掛載位置

```text
iso_postgres_development_data -> /var/lib/postgresql/data
iso_postgres_test_data        -> /var/lib/postgresql/data
iso_postgres_production_data  -> /var/lib/postgresql/data
iso_mysql_legacy_data         -> /var/lib/mysql
```
