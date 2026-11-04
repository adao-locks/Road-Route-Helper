# Road Route Helper: resumo técnico

Aplicação ASP.NET Core 10 (MVC e API REST) para planejar viagens de moto. Roda só localmente, sem login, e guarda as viagens em um arquivo JSON. Antes de publicar, é preciso concluir a etapa 5 do [spec de melhorias](../docs/superpowers/specs/2026-11-04-melhorias-road-route-helper-design.md).

## Estrutura

```
Road Route Helper/
├── Domain/
│   ├── Trip.cs                  # Viagem; DeepClone e ToCopy
│   ├── Stop.cs                  # Parada e enum StopCategory
│   ├── RoutePoint.cs            # Ponto do traçado
│   └── StopCategoryStyles.cs    # Rótulo, letra e cor de cada categoria
├── Application/
│   ├── DTOs/                    # TripDto (com validação) e TripMapper
│   ├── Interfaces/              # ITripRepository
│   ├── Posters/                 # PosterSvgBuilder
│   ├── Exports/                 # GpxBuilder
│   └── Formatting/              # DurationFormatter e FileNames
├── Infrastructure/
│   ├── JsonTripRepository.cs    # Persistência em arquivo JSON
│   └── TripStoreException.cs
├── Controllers/
│   ├── TripsController.cs       # API REST
│   ├── TripsWebController.cs    # Páginas, poster e GPX
│   └── HomeController.cs        # Painel
├── Views/                       # Painel, criação, edição e visualização
└── wwwroot/js/
    ├── trip-create.js           # Mapa, paradas, rota e salvamento
    ├── geocoding.js             # Busca de endereços
    ├── poster-png.js            # Conversão do poster para PNG
    └── dom.js                   # Helpers compartilhados

Road Route Helper.Tests/         # xUnit, WebApplicationFactory e Playwright
```

## Endereços

| Rota | Função |
|---|---|
| `GET /` | Painel com as viagens |
| `GET /Trips/Create` | Nova viagem |
| `GET /Trips/Edit/{id}` | Editar viagem |
| `GET /Trips/View/{id}` | Ver viagem |
| `GET /Trips/View/{id}/poster.svg` | Poster em SVG |
| `GET /Trips/View/{id}/route.gpx` | Exportação GPX |
| `GET`, `POST /api/trips` | Listar e criar |
| `GET`, `PUT`, `DELETE /api/trips/{id}` | Ler, atualizar e excluir |
| `POST /api/trips/{id}/duplicate` | Duplicar |

## Regras importantes

- **Início e destino:** a primeira parada é o início e a última é o destino.
- **Validação:** o nome tem de 1 a 120 caracteres, as coordenadas precisam ser válidas e a data final não pode ser anterior à inicial. Erros voltam como 400 com mensagem em português.
- **Cultura:** números em SVG, GPX e JavaScript usam sempre cultura invariante. Textos exibidos ao usuário usam pt-BR.
- **Texto do usuário:** nunca é inserido no DOM como HTML, somente com `textContent`.
- **Duração:** a API usa `hh:mm:ss`, com a parte de dias (`d.hh:mm:ss`) a partir de 24h. Na tela e no poster, aparece como `25h 00min`.
- **Rota:** qualquer mudança nas paradas invalida o traçado salvo e agenda um recálculo depois de 500 ms. Salvar espera um cálculo pendente. Se o serviço falhar, a viagem pode ser salva sem traçado.

## Persistência

O `JsonTripRepository` mantém as viagens em memória e regrava o arquivo inteiro a cada alteração: grava um temporário, substitui o original e mantém um `.bak`. Se a gravação falhar, a memória não muda. O formato é `{ "schemaVersion": 1, "trips": [...] }`.

Configuração em `appsettings.json`:

| Chave | Padrão |
|---|---|
| `Storage:TripsFilePath` | `App_Data/trips.json` |
| `Routing:ServiceUrl` | `https://router.project-osrm.org/route/v1` |
| `Geocoding:ServiceUrl` | `https://nominatim.openstreetmap.org/search` |

## Limitações conhecidas

- Sem login: quem acessa o app vê e altera todas as viagens.
- O arquivo JSON aceita um único processo do app e é regravado por inteiro a cada alteração.
- OSRM e Nominatim públicos têm limites de uso e não servem para produção.
- As bibliotecas de front-end vêm de CDNs, então o app precisa de internet.
- As fontes do poster em PNG dependem do sistema de quem exporta.

## Decisões

| Decisão | Motivo |
|---|---|
| JSON em vez de banco | Solução temporária para rodar localmente; trocar exige só outra implementação de `ITripRepository` |
| Login adiado | O ASP.NET Identity espera um banco real |
| PNG gerado no navegador | Evita uma biblioteca de imagens no servidor |
| Sem importação de GPX | Exportar já leva a rota ao GPS; importar exige tratar arquivos enviados e formatos variados |
