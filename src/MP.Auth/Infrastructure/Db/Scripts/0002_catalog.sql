-- 游戏注册表（平台治理，全局唯一 game_id）
-- 只存轻量元数据 + 状态，不负责版本发布 / 资源包 / 热更 URL

CREATE TABLE IF NOT EXISTS games (
    game_id      TEXT PRIMARY KEY,                          -- 全局唯一，建议小写短横线：match3 / flying-chess
    name         TEXT NOT NULL,                             -- 显示名
    status       TEXT NOT NULL DEFAULT 'active',            -- active / maintenance / offline
    sort_order   INT  NOT NULL DEFAULT 100,                 -- 大厅排序，越小越靠前
    icon_url     TEXT,                                      -- 可选，大厅图标
    min_client_ver TEXT,                                    -- 可选，最低客户端版本
    extra_json   JSONB,                                     -- 极少量平台级扩展
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_games_status_sort ON games (status, sort_order);

-- 预置示例（幂等）
INSERT INTO games (game_id, name, status, sort_order) VALUES
    ('match3',       '三消',   'active', 10),
    ('bubble',       '泡泡龙', 'active', 20),
    ('flying-chess', '飞行棋', 'active', 30),
    ('sudoku',       '数独',   'active', 40)
ON CONFLICT (game_id) DO NOTHING;
