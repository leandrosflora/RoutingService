-- Seed de malha logística para o Routing Service retornar rota no payload:
-- originNodeId = 33333333-3333-3333-3333-333333333333
-- destinationPostalCode = 05700000
--
-- Execute no banco PostgreSQL usado pelo serviço, no schema routing:
--   psql "Host=localhost Port=5432 Dbname=logistica_envios User=logistica options=--search_path=routing,public" \
--     -f database/seed-route-333-to-05700000.sql
--
-- Depois de executar, aguarde até 15 segundos para o RouteGraphRefreshWorker recarregar
-- a malha, ou reinicie o Routing Service. Se a resposta continuar com
-- "networkVersion": 1, o serviço ainda não recarregou a versão do banco ou está
-- executando com Routing:UseMockRepository=true.

BEGIN;

SET search_path TO routing, public;

-- Garante que a região usada por appsettings.json exista e força refresh do grafo.
INSERT INTO network_versions (region, version, updated_at)
VALUES ('Brasil Sudeste', 2, NOW())
ON CONFLICT (region) DO UPDATE
SET version = network_versions.version + 1,
    updated_at = NOW();

-- Origem usada pelo payload informado.
INSERT INTO logistics_nodes (
    id,
    code,
    name,
    region,
    time_zone_id,
    type,
    handling_minutes,
    is_active
)
VALUES (
    '33333333-3333-3333-3333-333333333333',
    'RIO-DLV-01',
    'Rio de Janeiro Delivery Station 01',
    'Brasil Sudeste',
    'America/Sao_Paulo',
    'LastMileStation',
    15,
    TRUE
)
ON CONFLICT (id) DO UPDATE
SET code = EXCLUDED.code,
    name = EXCLUDED.name,
    region = EXCLUDED.region,
    time_zone_id = EXCLUDED.time_zone_id,
    type = EXCLUDED.type,
    handling_minutes = EXCLUDED.handling_minutes,
    is_active = TRUE;

-- Nó destino que cobre o CEP 05700000 (São Paulo). O serviço calcula até o nó de
-- cobertura postal, não até o CEP individual.
INSERT INTO logistics_nodes (
    id,
    code,
    name,
    region,
    time_zone_id,
    type,
    handling_minutes,
    is_active
)
VALUES (
    '55555555-5555-5555-5555-555555555555',
    'SP-DLV-057',
    'São Paulo Delivery Station CEP 057',
    'Brasil Sudeste',
    'America/Sao_Paulo',
    'LastMileStation',
    15,
    TRUE
)
ON CONFLICT (id) DO UPDATE
SET code = EXCLUDED.code,
    name = EXCLUDED.name,
    region = EXCLUDED.region,
    time_zone_id = EXCLUDED.time_zone_id,
    type = EXCLUDED.type,
    handling_minutes = EXCLUDED.handling_minutes,
    is_active = TRUE;

-- Mantém o script idempotente para os registros controlados por este seed.
DELETE FROM lane_schedules
WHERE logistics_lane_id = 'dddddddd-dddd-dddd-dddd-ddddddddd057';

DELETE FROM logistics_lanes
WHERE id = 'dddddddd-dddd-dddd-dddd-ddddddddd057';

DELETE FROM postal_coverages
WHERE id = 'eeeeeeee-eeee-eeee-eeee-eeeeeeee0057';

-- Lane direta compatível com o pacote informado:
-- weightKg=0.450, cubicWeightKg=0.469333..., isFragile=false, isRestricted=false.
INSERT INTO logistics_lanes (
    id,
    origin_node_id,
    destination_node_id,
    carrier_code,
    mode,
    transit_minutes,
    maximum_weight_kg,
    maximum_cubic_weight_kg,
    supports_fragile_items,
    supports_restricted_items,
    status,
    version
)
VALUES (
    'dddddddd-dddd-dddd-dddd-ddddddddd057',
    '33333333-3333-3333-3333-333333333333',
    '55555555-5555-5555-5555-555555555555',
    'MELI_LOG',
    'Road',
    120,
    30.000,
    30.000,
    TRUE,
    FALSE,
    'Active',
    1
);

-- Terça-feira às 19:00 America/Sao_Paulo equivale a 22:00 UTC em 2026-06-30,
-- portanto fica após requestedAtUtc=2026-06-30T21:42:26.418Z.
INSERT INTO lane_schedules (
    id,
    logistics_lane_id,
    day_of_week,
    departure_time,
    is_active
)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaa05701', 'dddddddd-dddd-dddd-dddd-ddddddddd057', 'Tuesday', TIME '19:00:00', TRUE),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaa05702', 'dddddddd-dddd-dddd-dddd-ddddddddd057', 'Tuesday', TIME '23:00:00', TRUE),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaa05703', 'dddddddd-dddd-dddd-dddd-ddddddddd057', 'Wednesday', TIME '09:00:00', TRUE);

-- Cobertura postal que faz o CEP 05700000 resolver para o nó destino acima.
INSERT INTO postal_coverages (
    id,
    destination_node_id,
    postal_code_from,
    postal_code_to,
    priority
)
VALUES (
    'eeeeeeee-eeee-eeee-eeee-eeeeeeee0057',
    '55555555-5555-5555-5555-555555555555',
    5700000,
    5799999,
    1
);

COMMIT;

-- Conferência rápida: deve retornar a lane e a cobertura criadas por este seed.
SELECT
    lane.id AS lane_id,
    origin.code AS origin_code,
    destination.code AS destination_code,
    coverage.postal_code_from,
    coverage.postal_code_to,
    schedule.day_of_week,
    schedule.departure_time,
    version.version AS network_version
FROM logistics_lanes lane
JOIN logistics_nodes origin ON origin.id = lane.origin_node_id
JOIN logistics_nodes destination ON destination.id = lane.destination_node_id
JOIN postal_coverages coverage ON coverage.destination_node_id = destination.id
JOIN lane_schedules schedule ON schedule.logistics_lane_id = lane.id
JOIN network_versions version ON version.region = origin.region
WHERE lane.id = 'dddddddd-dddd-dddd-dddd-ddddddddd057'
ORDER BY schedule.day_of_week, schedule.departure_time;
