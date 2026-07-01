-- Verificação do trajeto:
-- originNodeId = 33333333-3333-3333-3333-333333333333
-- destinationPostalCode = 05700000
--
-- Execute no banco PostgreSQL usado pelo serviço, no schema routing:
--   psql "Host=localhost Port=5432 Dbname=logistica_envios User=logistica options=--search_path=routing,public" \
--     -f database/seed-route-333-to-05700000.sql
--
-- IMPORTANTE (revisado): este script foi originalmente escrito para popular um schema
-- routing vazio, e depois foi ajustado para adicionar uma lane extra (transportadora
-- fictícia "MELI_LOG") entre nodes já existentes no `logistica-envios-init.sql`. Essa
-- lane nunca funcionava de ponta a ponta: "MELI_LOG" não é um carrier cadastrado em
-- carrier.carriers (só CARRIER_1/CARRIER_2 existem), então o CarrierService sempre
-- respondia CarrierNotFound para ela, independente da rota do RoutingService.
--
-- A causa raiz de "no_options_available" para todos os produtos era outra: o
-- RoutingService nunca expunha um `service_level_code` de transportadora nas legs de
-- rota (só carrega `carrier_code`/`mode` de transporte), então ShippingPromiseService
-- forjava um serviceLevelCode a partir do modo de transporte (ex.: "Road"/"LastMile") ao
-- chamar o CarrierService — que nunca bate com os códigos reais ("same_day"/"standard"),
-- fazendo toda checagem de disponibilidade de transportadora falhar. Isso foi corrigido
-- adicionando a coluna `routing.logistics_lanes.service_level_code` (populada em
-- `logistica-envios-init.sql` com os códigos reais de carrier.carrier_service_levels) e
-- propagando esse valor real pelo RoutingService/ShippingPromiseService.
--
-- Com esse fix, a lane já cadastrada em logistica-envios-init.sql
-- (id 77777777-7777-7777-7777-777777777902, CARRIER_2/standard, direta de
-- 33333333-3333-3333-3333-333333333333 até 77777777-7777-7777-7777-777777777901) já é
-- suficiente para essa origem/CEP. Este script não insere mais nada — só verifica.

SET search_path TO routing, public;

SELECT
    lane.id AS lane_id,
    lane.carrier_code,
    lane.service_level_code,
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
WHERE lane.origin_node_id = '33333333-3333-3333-3333-333333333333'
  AND coverage.postal_code_from <= 5700000
  AND coverage.postal_code_to >= 5700000
ORDER BY schedule.day_of_week, schedule.departure_time;
