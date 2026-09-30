using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsoDocument.Api.Data.Migrations
{
    /// <summary>
    /// 新資料庫的初始資料：三家公司、各公司的品質系統，以及第一個 SYSTEM_ADMIN（admin / admin）。
    /// 啟動時會自動 Migrate，既有資料庫也會跑到這支，所以每段都可重複執行：
    /// 已存在的公司／品質系統直接跳過；資料庫已有任何 SYSTEM_ADMIN 或 empno = admin 時不建立 admin，
    /// 避免在已上線的資料庫多開一組預設帳密。
    /// </summary>
    public partial class SeedInitialData : Migration
    {
        private const string AdminEmpno = "admin";
        private const string AdminInitialPassword = "admin";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // companies.code 須符合 ^[A-Z0-9]{2,30}$（會進儲存路徑與浮水印），由英文名稱轉大寫、去空白而來。
            migrationBuilder.Sql("""
                INSERT INTO companies (code, name) VALUES
                    ('CAPITALBUS', '首都客運'),
                    ('TAIPEIBUS', '臺北客運'),
                    ('MTCBUS', '大都會客運')
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO iso_categories (company_id, name)
                SELECT c.id, v.name
                FROM companies c
                CROSS JOIN (VALUES ('ISO9001'), ('ISO39001'), ('ISO4001'), ('ESG')) AS v(name)
                WHERE c.code IN ('CAPITALBUS', 'TAIPEIBUS', 'MTCBUS')
                ON CONFLICT (company_id, name) DO NOTHING;
                """);

            // 雜湊在套用 migration 時才計算，與登入驗證同一個 PasswordHasher（PBKDF2），
            // 版控裡不會有固定的雜湊值。預設實作的 HashPassword 不使用 user 參數。
            var passwordDigest = new PasswordHasher<object>().HashPassword(new object(), AdminInitialPassword);

            migrationBuilder.Sql($"""
                DO $$
                DECLARE
                    v_company_id uuid;
                    v_dept_id uuid;
                BEGIN
                    IF EXISTS (SELECT 1 FROM users WHERE role = 'SYSTEM_ADMIN' OR empno = '{AdminEmpno}') THEN
                        RETURN;
                    END IF;

                    SELECT id INTO v_company_id FROM companies WHERE code = 'CAPITALBUS';
                    IF v_company_id IS NULL THEN
                        RETURN;
                    END IF;

                    INSERT INTO depts (company_id, name) VALUES (v_company_id, '資訊中心')
                    ON CONFLICT (company_id, name) DO NOTHING;
                    SELECT id INTO v_dept_id FROM depts WHERE company_id = v_company_id AND name = '資訊中心';

                    INSERT INTO users (company_id, dept_id, empno, name, role, password_digest, must_change_password)
                    VALUES (v_company_id, v_dept_id, '{AdminEmpno}', '系統管理員', 'SYSTEM_ADMIN', '{passwordDigest}', true);
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 種子資料一旦被文件、稽核紀錄等外鍵參照就無法安全刪除，rollback 時保留資料。
        }
    }
}
