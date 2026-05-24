DROP TABLE IF EXISTS visits CASCADE;
DROP TABLE IF EXISTS bookings CASCADE;
DROP TABLE IF EXISTS schedules CASCADE;
DROP TABLE IF EXISTS class_types CASCADE;
DROP TABLE IF EXISTS payments CASCADE;
DROP TABLE IF EXISTS memberships CASCADE;
DROP TABLE IF EXISTS membership_types CASCADE;
DROP TABLE IF EXISTS trainers CASCADE;
DROP TABLE IF EXISTS clients CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS roles CASCADE;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ─────────────────────────────────────────────
-- РОЛІ КОРИСТУВАЧІВ
-- ─────────────────────────────────────────────
CREATE TABLE roles (
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(50) NOT NULL UNIQUE,  -- Admin, Manager, Trainer, Client
    description TEXT
);

INSERT INTO roles (name, description) VALUES
    ('Admin',   'Повний доступ до системи'),
    ('Manager', 'Управління клієнтами, абонементами, платежами'),
    ('Trainer', 'Перегляд свого розкладу та клієнтів'),
    ('Client',  'Мобільний додаток — перегляд свого профілю');

-- ─────────────────────────────────────────────
-- КОРИСТУВАЧІ (всі ролі)
-- ─────────────────────────────────────────────
CREATE TABLE users (
    id            SERIAL PRIMARY KEY,
    email         VARCHAR(150) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    first_name    VARCHAR(100) NOT NULL,
    last_name     VARCHAR(100) NOT NULL,
    phone         VARCHAR(20),
    role_id       INTEGER NOT NULL REFERENCES roles(id),
    is_active     BOOLEAN DEFAULT TRUE,
    created_at    TIMESTAMP DEFAULT NOW(),
    updated_at    TIMESTAMP DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- ПРОФІЛІ КЛІЄНТІВ
-- ─────────────────────────────────────────────
CREATE TABLE clients (
    id            SERIAL PRIMARY KEY,
    user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    date_of_birth DATE,
    gender        VARCHAR(10),          -- Male, Female, Other
    address       TEXT,
    emergency_contact VARCHAR(150),
    health_notes  TEXT,                 -- медичні примітки
    photo_url     VARCHAR(500),
    created_at    TIMESTAMP DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- ПРОФІЛІ ТРЕНЕРІВ
-- ─────────────────────────────────────────────
CREATE TABLE trainers (
    id              SERIAL PRIMARY KEY,
    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    specialization  VARCHAR(200),       -- Йога, Кардіо, Силові тренування...
    experience_years INTEGER DEFAULT 0,
    bio             TEXT,
    hourly_rate     DECIMAL(10,2),
    photo_url       VARCHAR(500),
    created_at      TIMESTAMP DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- ТИПИ АБОНЕМЕНТІВ
-- ─────────────────────────────────────────────
CREATE TABLE membership_types (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(100) NOT NULL,  -- Базовий, Стандарт, Преміум
    description     TEXT,
    duration_days   INTEGER NOT NULL,       -- 30, 90, 365 днів
    price           DECIMAL(10,2) NOT NULL,
    max_visits      INTEGER,                -- NULL = необмежено
    includes_pool   BOOLEAN DEFAULT FALSE,
    includes_gym    BOOLEAN DEFAULT TRUE,
    includes_classes BOOLEAN DEFAULT FALSE,
    is_active       BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMP DEFAULT NOW()
);

INSERT INTO membership_types (name, description, duration_days, price, max_visits, includes_pool, includes_gym, includes_classes) VALUES
    ('Базовий',   'Тільки тренажерний зал', 30, 500.00, 12, FALSE, TRUE, FALSE),
    ('Стандарт',  'Зал + групові заняття',  30, 800.00, NULL, FALSE, TRUE, TRUE),
    ('Преміум',   'Зал + заняття + басейн', 30, 1200.00, NULL, TRUE, TRUE, TRUE),
    ('Річний',    'Повний доступ на рік',   365, 9000.00, NULL, TRUE, TRUE, TRUE);

-- ─────────────────────────────────────────────
-- АБОНЕМЕНТИ КЛІЄНТІВ
-- ─────────────────────────────────────────────
CREATE TABLE memberships (
    id                   SERIAL PRIMARY KEY,
    client_id            INTEGER NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    membership_type_id   INTEGER NOT NULL REFERENCES membership_types(id),
    start_date           DATE NOT NULL,
    end_date             DATE NOT NULL,
    visits_used          INTEGER DEFAULT 0,
    status               VARCHAR(20) DEFAULT 'Active',  -- Active, Expired, Frozen, Cancelled
    frozen_from          DATE,
    frozen_to            DATE,
    notes                TEXT,
    created_at           TIMESTAMP DEFAULT NOW(),
    updated_at           TIMESTAMP DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- ПЛАТЕЖІ
-- ─────────────────────────────────────────────
CREATE TABLE payments (
    id              SERIAL PRIMARY KEY,
    client_id       INTEGER NOT NULL REFERENCES clients(id),
    membership_id   INTEGER REFERENCES memberships(id),
    amount          DECIMAL(10,2) NOT NULL,
    payment_date    TIMESTAMP DEFAULT NOW(),
    payment_method  VARCHAR(50),        -- Cash, Card, Online
    status          VARCHAR(20) DEFAULT 'Completed',  -- Completed, Pending, Refunded
    description     TEXT,
    created_by      INTEGER REFERENCES users(id)      -- хто прийняв оплату
);

-- ─────────────────────────────────────────────
-- ГРУПОВІ ЗАНЯТТЯ / РОЗКЛАД
-- ─────────────────────────────────────────────
CREATE TABLE class_types (
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(100) NOT NULL,  -- Йога, Пілатес, Зумба...
    description TEXT,
    duration_minutes INTEGER DEFAULT 60,
    max_participants INTEGER DEFAULT 20,
    color       VARCHAR(7) DEFAULT '#6C63FF'  -- колір в розкладі
);

INSERT INTO class_types (name, description, duration_minutes, max_participants, color) VALUES
    ('Йога',       'Розтяжка та медитація',     60, 15, '#a78bfa'),
    ('Пілатес',    'Зміцнення м''язів кору',    60, 12, '#f472b6'),
    ('Зумба',      'Танцювальне кардіо',         60, 25, '#fb923c'),
    ('Кардіо',     'Інтенсивне кардіо тренування', 45, 20, '#f87171'),
    ('Силові',     'Тренування з вагою',         60, 15, '#60a5fa'),
    ('Стретчинг',  'Розтяжка для всіх рівнів',  45, 20, '#34d399');

CREATE TABLE schedules (
    id              SERIAL PRIMARY KEY,
    class_type_id   INTEGER NOT NULL REFERENCES class_types(id),
    trainer_id      INTEGER NOT NULL REFERENCES trainers(id),
    start_datetime  TIMESTAMP NOT NULL,
    end_datetime    TIMESTAMP NOT NULL,
    room            VARCHAR(50),
    status          VARCHAR(20) DEFAULT 'Scheduled',  -- Scheduled, Completed, Cancelled
    notes           TEXT,
    created_at      TIMESTAMP DEFAULT NOW()
);

-- ─────────────────────────────────────────────
-- ЗАПИСИ НА ЗАНЯТТЯ
-- ─────────────────────────────────────────────
CREATE TABLE bookings (
    id              SERIAL PRIMARY KEY,
    schedule_id     INTEGER NOT NULL REFERENCES schedules(id) ON DELETE CASCADE,
    client_id       INTEGER NOT NULL REFERENCES clients(id),
    status          VARCHAR(20) DEFAULT 'Booked',  -- Booked, Attended, Cancelled, NoShow
    booked_at       TIMESTAMP DEFAULT NOW(),
    cancelled_at    TIMESTAMP,
    UNIQUE(schedule_id, client_id)
);

-- ─────────────────────────────────────────────
-- ВІДВІДУВАННЯ (сканування на вході)
-- ─────────────────────────────────────────────
CREATE TABLE visits (
    id          SERIAL PRIMARY KEY,
    client_id   INTEGER NOT NULL REFERENCES clients(id),
    check_in    TIMESTAMP DEFAULT NOW(),
    check_out   TIMESTAMP,
    notes       TEXT
);

-- ─────────────────────────────────────────────
-- ІНДЕКСИ ДЛЯ ПРОДУКТИВНОСТІ
-- ─────────────────────────────────────────────
CREATE INDEX idx_users_email         ON users(email);
CREATE INDEX idx_users_role          ON users(role_id);
CREATE INDEX idx_memberships_client  ON memberships(client_id);
CREATE INDEX idx_memberships_status  ON memberships(status);
CREATE INDEX idx_schedules_datetime  ON schedules(start_datetime);
CREATE INDEX idx_bookings_schedule   ON bookings(schedule_id);
CREATE INDEX idx_bookings_client     ON bookings(client_id);
CREATE INDEX idx_payments_client     ON payments(client_id);
CREATE INDEX idx_visits_client       ON visits(client_id);

-- ─────────────────────────────────────────────
-- ТЕСТОВІ ДАНІ
-- ─────────────────────────────────────────────

-- ─────────────────────────────────────────────
-- ТЕСТОВІ ДАНІ (REAL BCRYPT HASHES)
-- ─────────────────────────────────────────────

INSERT INTO users (email, password_hash, first_name, last_name, phone, role_id)
VALUES
('admin@fitness.com', crypt('Admin123!', gen_salt('bf')), 'Євгенія', 'Варинська', '+380991234567', 1),
('manager@fitness.com', crypt('Manager123!', gen_salt('bf')), 'Марина', 'Менеджерова', '+380992345678', 2),
('trainer1@fitness.com', crypt('Trainer123!', gen_salt('bf')), 'Іван', 'Тренеренко', '+380993456789', 3),
('trainer2@fitness.com', crypt('Trainer123!', gen_salt('bf')), 'Ольга', 'Спортова', '+380994567890', 3),
('client1@gmail.com', crypt('Client123!', gen_salt('bf')), 'Анна', 'Клієнтова', '+380995678901', 4),
('client2@gmail.com', crypt('Client123!', gen_salt('bf')), 'Петро', 'Фітнесенко', '+380996789012', 4),
('client3@gmail.com', crypt('Client123!', gen_salt('bf')), 'Соломія', 'Здорова', '+380997890123', 4);


INSERT INTO clients (user_id, date_of_birth, gender) VALUES
    (5, '1995-03-15', 'Female'),
    (6, '1988-07-22', 'Male'),
    (7, '2000-11-08', 'Female');

-- Абонементи
INSERT INTO memberships (client_id, membership_type_id, start_date, end_date, status) VALUES
    (1, 2, '2026-02-01', '2026-03-01', 'Active'),
    (2, 3, '2026-01-15', '2026-02-15', 'Expired'),
    (3, 1, '2026-02-10', '2026-03-10', 'Active');

-- Платежі
INSERT INTO payments (client_id, membership_id, amount, payment_method, description, created_by) VALUES
    (1, 1, 800.00,  'Card', 'Оплата абонементу Стандарт',  2),
    (2, 2, 1200.00, 'Cash', 'Оплата абонементу Преміум',   2),
    (3, 3, 500.00,  'Card', 'Оплата абонементу Базовий',   2);

COMMENT ON TABLE users           IS 'Всі користувачі системи';
COMMENT ON TABLE clients         IS 'Профілі клієнтів фітнес клубу';
COMMENT ON TABLE trainers        IS 'Профілі тренерів';
COMMENT ON TABLE memberships     IS 'Абонементи клієнтів';
COMMENT ON TABLE membership_types IS 'Типи абонементів';
COMMENT ON TABLE payments        IS 'Платежі за абонементи';
COMMENT ON TABLE schedules       IS 'Розклад групових занять';
COMMENT ON TABLE bookings        IS 'Записи клієнтів на заняття';
COMMENT ON TABLE visits          IS 'Відвідування клубу';