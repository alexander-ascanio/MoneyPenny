-- Script de desarrollo: tabla tickets compatible con MoneyPenny (snake_case PostgreSQL).
-- Si tu base TeamSupport usa otro esquema, ajusta Models/Tickets/Ticket.cs y TicketsDbContext.

CREATE DATABASE tickets_source_db;

\c tickets_source_db

CREATE TABLE IF NOT EXISTS tickets (
    id SERIAL PRIMARY KEY,
    number VARCHAR(50) NOT NULL,
    title VARCHAR(500) NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    status VARCHAR(50) NOT NULL DEFAULT 'Open',
    priority VARCHAR(50) NOT NULL DEFAULT 'Normal',
    assignee VARCHAR(200),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP
);

INSERT INTO tickets (number, title, description, status, priority, assignee, created_at)
VALUES
    ('TS-1001', 'Error al iniciar sesión', 'El usuario no puede acceder al portal tras el último despliegue.', 'Open', 'High', 'Soporte N1', NOW() - INTERVAL '2 days'),
    ('TS-1002', 'Consulta sobre facturación', 'Cliente solicita detalle de cargos del último mes.', 'In Progress', 'Normal', 'Soporte N2', NOW() - INTERVAL '1 day'),
    ('TS-1003', 'Integración API caída', 'Timeout en endpoint de sincronización con ERP.', 'Resolved', 'Critical', 'DevOps', NOW() - INTERVAL '5 hours');
