# RoutingService

Microserviço de roteirização logística em **.NET 8** alinhado à documentação arquitetural do repositório [`leandrosflora/logistica-envios-demo-arch`](https://github.com/leandrosflora/logistica-envios-demo-arch).

## Responsabilidade

O serviço calcula rotas logísticas entre uma origem operacional, representada por um fulfillment center ou nó da malha, e um destino identificado pelo CEP do buyer. O cálculo considera:

- malha logística proprietária;
- hubs intermediários;
- janelas semanais de trânsito;
- SLA estimado de entrega;
- modalidades de transporte disponíveis em cada lane;
- restrições físicas do pacote;
- versão atual da malha logística.

O serviço domina os conceitos de `Route` e `LogisticNetwork` e não publica nem consome eventos Kafka, conforme a spec do Routing Service no repositório de arquitetura.

## API HTTP pública

A superfície pública segue a documentação de referência do Routing Service:

| Método | Endpoint | Descrição |
| --- | --- | --- |
| `POST` | `/v1/routes/calculate` | Calcula rotas para origem, destino e modalidade disponível na malha. |
| `GET` | `/v1/routes/{routeId}` | Retorna detalhes de uma rota calculada anteriormente pela instância. |

A API também expõe `GET /health` para health check operacional.

## `POST /v1/routes/calculate`

Calcula opções de rota para uma origem e CEP de destino.

### Headers

| Header | Obrigatório | Descrição |
| --- | --- | --- |
| `X-Correlation-Id` | Não | Identificador de correlação propagado em logs estruturados. |

### Request

```json
{
  "originNodeId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "destinationPostalCode": "12345-678",
  "package": {
    "weightKg": 2.0,
    "cubicWeightKg": 1.0,
    "isFragile": false,
    "isRestricted": false
  },
  "requestedAtUtc": "2026-06-15T10:00:00Z",
  "maxOptions": 3
}
```

### Response `200 OK`

```json
{
  "networkVersion": 42,
  "source": "Calculated",
  "routes": [
    {
      "routeId": "route_abc123",
      "originNodeId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "destinationNodeId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      "estimatedDepartureAt": "2026-06-15T10:00:00Z",
      "estimatedArrivalAt": "2026-06-15T11:35:00Z",
      "totalElapsedMinutes": 95,
      "legs": [
        {
          "laneId": "dddddddd-dddd-dddd-dddd-dddddddddddd",
          "originNodeId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          "originCode": "ORI",
          "destinationNodeId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          "destinationCode": "DST",
          "carrierCode": "MEL",
          "mode": "Road",
          "departureAt": "2026-06-15T10:00:00Z",
          "arrivalAt": "2026-06-15T11:30:00Z",
          "transitMinutes": 90
        }
      ]
    }
  ]
}
```

## `GET /v1/routes/{routeId}`

Retorna os detalhes de uma rota calculada previamente por `POST /v1/routes/calculate`.

### Response `200 OK`

```json
{
  "routeId": "route_abc123",
  "originNodeId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "destinationNodeId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "estimatedDepartureAt": "2026-06-15T10:00:00Z",
  "estimatedArrivalAt": "2026-06-15T11:35:00Z",
  "totalElapsedMinutes": 95,
  "legs": []
}
```

### Response `404 Not Found`

```json
{
  "error": "Route not found"
}
```

## Arquitetura interna

| Camada | Responsabilidade |
| --- | --- |
| `Api` | Define endpoints HTTP públicos documentados e traduz erros em respostas HTTP. |
| `Contracts` | DTOs explícitos de entrada e saída. |
| `Application` | Orquestra validação, cache, cálculo de rotas e consulta de rota calculada. |
| `Domain` | Modela nós logísticos, lanes, agendas, cobertura postal, versões de rede e enums. |
| `Graph` | Snapshot imutável da malha e store em memória. |
| `Infrastructure` | Persistência da malha, cache Redis/memória, worker de refresh e store em memória para rotas calculadas. |

## Execução local

```bash
dotnet restore
dotnet run
```

Para desenvolvimento sem PostgreSQL/Redis, configure:

```bash
export Routing__UseMockRepository=true
```

## Validação

```bash
dotnet restore
dotnet build
dotnet test
```
