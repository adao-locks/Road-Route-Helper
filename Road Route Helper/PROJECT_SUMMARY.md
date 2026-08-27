# Road Route Helper - Resumo Técnico Completo

## 1. Visão Geral do Sistema

**Road Route Helper** é uma aplicação web moderna para planejamento e organização de viagens, rotas de moto e aventuras. Construída com **ASP.NET Core 10**, **C#**, seguindo princípios de **Clean Architecture** com separação em camadas (Domain, Application, Infrastructure, Presentation).

### Objetivo Principal
Permitir usuários criar, editar, duplicar e deletar viagens com:
- Múltiplas paradas com categorias (restaurante, foto, gasolina, hotel, etc)
- Cálculo automático de rotas usando OSRM/Leaflet
- Visualização em mapa interativo
- Geração de **poster visual premium** com traçado, markers e legenda
- Dashboard para gerenciar todas as viagens

---

## 2. Arquitetura e Camadas

```
Road Route Helper/
├── Domain/                    # Modelos de domínio puros
│   ├── Trip.cs               # Entidade viagem
│   ├── Stop.cs               # Entidade parada + enum StopCategory
│   └── RoutePoint.cs         # Ponto de geometria de rota
│
├── Application/              # Lógica de aplicação
│   ├── Interfaces/
│   │   └── ITripRepository.cs
│   └── DTOs/
│       └── TripDto.cs        # DTO para transferência de dados
│
├── Infrastructure/           # Implementação de infraestrutura
│   └── InMemoryTripRepository.cs  # Repositório em memória (não persistente)
│
├── Controllers/              # Controladores MVC/API
│   ├── TripsController.cs     # API REST (/api/trips)
│   ├── TripsWebController.cs  # Páginas Web (Create/Edit/View/Poster)
│   └── HomeController.cs      # Dashboard
│
├── Views/                    # Razor Views (MVC)
│   ├── Home/
│   │   └── Index.cshtml      # Dashboard (lista de trips)
│   ├── Trips/
│   │   ├── Create.cshtml     # Criar/editar viagem com mapa
│   │   ├── Edit.cshtml       # Editar viagem existente
│   │   └── View.cshtml       # Visualizar viagem e poster
│   └── Shared/
│       ├── _Layout.cshtml    # Layout principal
│       └── _ViewStart.cshtml # Configuração de views
│
├── wwwroot/                  # Assets estáticos
│   ├── css/
│   │   └── site.css          # Estilos customizados
│   └── js/
│       └── trip-create.js    # Script de mapa e stops (Leaflet)
│
├── Program.cs               # Configuração e DI
└── appsettings.json         # Configurações
```

---

## 3. Modelos de Domínio

### 3.1 Domain/Stop.cs
```csharp
public enum StopCategory
{
	Restaurant,          // R / Vermelho
	PhotoSpot,           // P / Âmbar
	GasStation,          // G / Laranja
	Hotel,               // H / Ciano
	TouristAttraction,   // T / Violeta
	CoffeeShop,          // C / Marrom
	MotorcycleStop,      // M / Verde
	Maintenance,         // ! / Vermelho
	Custom               // • / Cinza
}

public class Stop
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public StopCategory Category { get; set; }
	public double Latitude { get; set; }
	public double Longitude { get; set; }
	public string Notes { get; set; }
	public int Order { get; set; }  // Ordem na rota
}
```

### 3.2 Domain/Trip.cs
```csharp
public class Trip
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public DateTimeOffset? StartDate { get; set; }
	public DateTimeOffset? EndDate { get; set; }

	// Starting point
	public string StartingPointName { get; set; }
	public double? StartingLatitude { get; set; }
	public double? StartingLongitude { get; set; }

	// Destination
	public string DestinationName { get; set; }
	public double? DestinationLatitude { get; set; }
	public double? DestinationLongitude { get; set; }

	// Stops and route
	public List<Stop> Stops { get; set; }
	public List<RoutePoint> RouteGeometry { get; set; }  // Traçado real da rota

	// Computed
	public double TotalDistanceKm { get; set; }
	public TimeSpan? EstimatedDuration { get; set; }
	public string Notes { get; set; }
}
```

### 3.3 Domain/RoutePoint.cs
```csharp
public class RoutePoint
{
	public double Latitude { get; set; }
	public double Longitude { get; set; }
}
```

---

## 4. Camada de Aplicação

### 4.1 Application/Interfaces/ITripRepository.cs
```csharp
public interface ITripRepository
{
	Task<IEnumerable<Trip>> GetAllAsync();
	Task<Trip?> GetByIdAsync(Guid id);
	Task<Trip> CreateAsync(Trip trip);
	Task<Trip?> UpdateAsync(Trip trip);
	Task<bool> DeleteAsync(Guid id);
	Task<Trip?> DuplicateAsync(Guid id);
}
```

### 4.2 Application/DTOs/TripDto.cs
```csharp
public class StopDto
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public StopCategory Category { get; set; }
	public double Latitude { get; set; }
	public double Longitude { get; set; }
	public string Notes { get; set; }
	public int Order { get; set; }
}

public class TripDto
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public DateTimeOffset? StartDate { get; set; }
	public DateTimeOffset? EndDate { get; set; }
	public List<StopDto> Stops { get; set; }
	public List<RoutePointDto> RouteGeometry { get; set; }
	public double TotalDistanceKm { get; set; }
	public TimeSpan? EstimatedDuration { get; set; }
	// ... outros campos
}

public class RoutePointDto
{
	public double Latitude { get; set; }
	public double Longitude { get; set; }
}
```

---

## 5. Camada de Infraestrutura

### 5.1 Infrastructure/InMemoryTripRepository.cs
- Implementação em memória usando **ConcurrentDictionary<Guid, Trip>**
- Thread-safe mas **não persistente** (dados perdidos ao reiniciar)
- Seed com uma viagem de exemplo
- Métodos: GetAll, GetById, Create, Update, Delete, Duplicate

**Limitação**: Trocar para EF Core + banco de dados real (SQLite/Postgres) para persistência.

---

## 6. API REST Endpoints

### 6.1 Trips API (Controllers/TripsController.cs)
```
GET    /api/trips              → Lista todas as viagens (TripDto[])
GET    /api/trips/{id}         → Retorna viagem específica (TripDto)
POST   /api/trips              → Cria nova viagem (TripDto no body)
PUT    /api/trips/{id}         → Atualiza viagem (TripDto no body)
DELETE /api/trips/{id}         → Deleta viagem
POST   /api/trips/{id}/duplicate → Duplica viagem
```

### 6.2 Web Pages (Controllers/TripsWebController.cs)
```
GET /Trips/Create              → Página criar trip (formulário + mapa)
GET /Trips/Edit/{id}           → Página editar trip existente
GET /Trips/View/{id}           → Visualizar trip (mapa + detalhes)
GET /Trips/View/{id}/poster.svg → Gera poster SVG da rota
```

### 6.3 Dashboard (Controllers/HomeController.cs)
```
GET /                          → Dashboard com lista de trips (cards)
```

---

## 7. Fluxo de Dados - Criar/Editar Viagem

### 7.1 Criar Viagem (Create)
```
1. Usuário acessa /Trips/Create
   ↓
2. TripsWebController.Create() retorna Views/Trips/Create.cshtml
   ↓
3. JavaScript (wwwroot/js/trip-create.js) inicializa:
   - Leaflet map no id="map"
   - Estado em window._tripCreate: { map, stops[], markers[], routingControl }
   - routingServiceUrl carregado de ViewData (configurável via appsettings)
   ↓
4. Usuário adiciona stops no mapa:
   - Click "Add" → addStopFromInputs() → novo Stop objeto
   - Stop aparece em lista (stopsList) com drag-drop via SortableJS
   - updateRouting() dispara Leaflet Routing Machine automaticamente
   ↓
5. Leaflet Routing Machine (OSRM) retorna rota com geometria
   - routingControl.on('routesfound', ...) captura route.coordinates
   - Converte para array { latitude, longitude } → window._lastRoute.geometry
   - Desenha polyline no mapa com gradient amarelo/laranja
   ↓
6. Usuário clica "Calcular rota" (opcional):
   - calculateRoute() espera até 10s pela geometria ser preenchida
   - Feedback visual: botão fica "Calculando..."
   ↓
7. Usuário clica "Salvar viagem":
   - saveTrip() monta TripDto com:
	 * nome, descrição, datas
	 * stops[] reordenado
	 * routeGeometry (do window._lastRoute.geometry)
	 * totalDistanceKm, estimatedDuration
   - POST /api/trips → TripsController.Create(TripDto)
   - Servidor mapeia DTO → Trip domain
   - Repositório armazena em memória
   - Retorna 201 Created com Trip criado
   - Página redireciona para / (Dashboard)
```

### 7.2 Editar Viagem (Edit)
```
1. Usuário clica "Edit" no card da viagem
   ↓
2. GET /Trips/Edit/{id} → TripsWebController.Edit({id})
   ↓
3. Busca Trip do repositório via ITripRepository.GetByIdAsync(id)
   ↓
4. Retorna Views/Trips/Edit.cshtml com Trip como model
   ↓
5. View injeta window._tripCreate.initialData com:
   - Trip nome, descrição, datas
   - stops[] pré-preenchido do trip
   - routeGeometry (se existir) carregado em window._lastRoute
   ↓
6. trip-create.js carrega dados iniciais:
   - Renderiza stops na lista
   - Se routeGeometry existe, desenha polyline diretamente (sem recalcular)
   - Caso contrário, dispara updateRouting() automaticamente
   ↓
7. Usuário pode:
   - Editar nome/descrição/datas
   - Adicionar/remover/reordenar stops (como em Create)
   - Clicar "Calcular rota" para recalcular (recaptura geometry)
   ↓
8. Clica "Salvar alterações":
   - saveTrip() monta TripDto (preserva ID do trip)
   - PUT /api/trips/{id} → TripsController.Update({id}, TripDto)
   - Servidor atualiza Trip no repositório
   - Redireciona para /
```

---

## 8. Visualização de Viagem e Poster

### 8.1 Visualizar Viagem (View)
```
GET /Trips/View/{id}
  ↓
Retorna Views/Trips/View.cshtml com Trip model
  ↓
View.cshtml renderiza:
  - Mapa (Leaflet) mostrando:
	• Se RouteGeometry existe → desenha polyline detalhada
	• Se não → dispara Leaflet Routing Machine para recalcular
	• Markers dos stops com popups
  - Painel lateral com detalhes (descrição, distância, duração)
  - Botões: Back, Duplicate, Generate Route Image
```

### 8.2 Gerador de Poster (Poster SVG)
```
GET /Trips/View/{id}/poster.svg
  ↓
TripsWebController.PosterSvg({id})
  ↓
1. Busca Trip e stops ordenados
2. Usa RouteGeometry se disponível (senão usa stops)
3. **Simplificação**: Aplica Ramer-Douglas-Peucker (8m tolerância)
   → Reduz número de pontos mantendo forma
4. **Projeção**: Web Mercator (EPSG:3857)
   → Converte lat/lng para metros (precisão em áreas grandes)
5. **Cálculo de escala/centralização**:
   - Bounding box em mercator
   - Dimensões: 1200x800 pixels
   - Padding automático: 5% do menor lado (mín 40px)
   - Centralização com offsets
6. **SVG Output** com:
   - Fundo em gradient (azul escuro degradê)
   - **Título** principal (nome trip)
   - **Subtítulo** (descrição ou datas)
   - **Polyline** com gradient (amarelo → laranja) + brilho (feGaussianBlur)
   - **Markers** nos stops:
	 • Círculo colorido com ícone/letra por categoria
	 • Número da parada abaixo
   - **Legenda** (rodapé): distância e duração
   - **Mini-map inset** (canto inferior direito):
	 • Geometria simplificada em escala reduzida
	 • Mesmo gradient da polyline
```

**Exemplo de cores de categoria no poster:**
- Restaurant (R) → Vermelho
- GasStation (G) → Laranja
- PhotoSpot (P) → Âmbar
- Hotel (H) → Ciano
- TouristAttraction (T) → Violeta
- CoffeeShop (C) → Marrom
- MotorcycleStop (M) → Verde
- Maintenance (!) → Vermelho
- Custom (•) → Cinza

---

## 9. Componentes Front-End

### 9.1 wwwroot/js/trip-create.js
**Responsabilidades:**
- Inicializa e gerencia mapa Leaflet
- Estado global: window._tripCreate { map, stops[], markers[], routingControl, routingServiceUrl }
- Funções expostas globalmente:
  - `addStopFromInputs()` — adiciona parada no click do botão
  - `calculateRoute()` — força cálculo de rota com UI feedback
  - `removeStop(id)` — remove parada
  - `saveTrip()` — POST/PUT da viagem
- Inicializa de dados salvos via window._tripCreate.initialData (para edição)
- Integração com Leaflet Routing Machine (OSRM)
  - Detecta routesfound event
  - Extrai geometria (route.coordinates) e distância/duração
  - Armazena em window._lastRoute { geometry, distanceKm, duration }

### 9.2 Views/Trips/Create.cshtml
```html
<form id="tripForm">
  <input id="name" />
  <textarea id="description"></textarea>
  <input type="date" id="startDate" />
  <input type="date" id="endDate" />
  <button onclick="calculateRoute()">Calcular rota</button>
  <button onclick="saveTrip()">Salvar viagem</button>
</form>
<div id="map" style="height:480px"></div>
<input id="stopName" />
<button onclick="addStopFromInputs()">Add</button>
<ul id="stopsList"></ul>
```

### 9.3 Views/Shared/_Layout.cshtml
**CDNs carregados:**
- Bootstrap 5.3.2
- Leaflet 1.9.4
- Leaflet Routing Machine 3.2.12
- SortableJS 1.15.0

---

## 10. Configuração (Program.cs)

### 10.1 Registros de DI
```csharp
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();

// Infraestrutura
builder.Services.AddSingleton<InMemoryTripRepository>();
builder.Services.AddSingleton<ITripRepository>(sp => 
	sp.GetRequiredService<InMemoryTripRepository>());

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");
```

### 10.2 Configuração (appsettings.json)
```json
{
  "Routing": {
	"ServiceUrl": "https://router.project-osrm.org/route/v1",
	"Provider": "osrm"
  }
}
```

---

## 11. Fluxo de Captura de Geometria (Crítico)

```
Cliente (wwwroot/js/trip-create.js):
  1. Leaflet Routing Machine faz chamada HTTP para roteador (OSRM)
  2. Roteador retorna { routes: [{ coordinates: [[lng,lat],...], summary: { totalDistance, totalTime } }] }
  3. Script converte coordinates → { latitude, longitude } array
  4. Armazena em window._lastRoute = { geometry: [...], distanceKm, duration }
  5. Ao salvar (saveTrip):
	 - Inclui routeGeometry: window._lastRoute.geometry no DTO
	 - POST /api/trips com DTO

Servidor (TripsController):
  1. Recebe TripDto com routeGeometry (RoutePointDto[])
  2. Mapeia DTO.routeGeometry → Trip.RouteGeometry (RoutePoint[])
  3. Armazena no repositório
  4. Ao visualizar: RouteGeometry é carregado e usado para poster/mapa
```

---

## 12. Fluxo de Geração do Poster

```
GET /Trips/View/{id}/poster.svg
  ↓
TripsWebController.PosterSvg(id)
  ↓
  1. Carrega Trip e stops ordenados
  2. Extrai RouteGeometry (se vazio, usa stops como fallback)
  3. Simplificação RDP (8m tolerância) em Mercator
  4. Converte para Mercator (lat/lng → metros)
  5. Calcula bounding box + escala + centralização
  6. Constrói SVG com gradients, polyline, markers, legenda, mini-map
  7. Retorna Content(svg, "image/svg+xml")
  ↓
Navegador renderiza SVG
  ↓
Usuário pode:
  - Abrir em nova aba
  - Salvar como SVG (arquivo vetorial)
  - Fazer print/screenshot → PNG/PDF
  - Converter com ferramentas online SVG→PNG
```

---

## 13. Limitações Conhecidas

| Limitação | Impacto | Resolução Sugerida |
|-----------|---------|-------------------|
| **Em memória** | Dados perdidos ao reiniciar | Migrar para EF Core + SQLite/Postgres |
| **Roteador público OSRM** | Limites de taxa; SLA baixo | Hospedar OSRM próprio ou usar Mapbox/GraphHopper pagos |
| **Sem autenticação** | Qualquer usuário vê todas as viagens | Adicionar Identity/JWT |
| **Sem sincronização em nuvem** | Dados locais apenas | Integrar Azure Blob/AWS S3 |
| **Poster PNG/JPG server-side** | Atualmente apenas SVG | Adicionar ImageSharp/SkiaSharp para converter |
| **RouteGeometry grande** | JSON volumoso em trips grandes | Aplicar compressão ou paginação |
| **Sem suporte a múltiplos usuários** | Sem conceito de "meu user" | Adicionar user context e filtros |

---

## 14. Arquivos Principais

| Arquivo | Responsabilidade | LOC |
|---------|------------------|-----|
| Domain/Trip.cs | Modelo de trip | ~30 |
| Domain/Stop.cs | Modelo de stop + categoria | ~25 |
| Domain/RoutePoint.cs | Ponto de rota | ~7 |
| Application/Interfaces/ITripRepository.cs | Contrato repository | ~12 |
| Application/DTOs/TripDto.cs | DTO transferência | ~45 |
| Infrastructure/InMemoryTripRepository.cs | Repositório em memória | ~110 |
| Controllers/TripsController.cs | API REST | ~140 |
| Controllers/TripsWebController.cs | Web + Poster | ~200 |
| Controllers/HomeController.cs | Dashboard | ~12 |
| wwwroot/js/trip-create.js | Mapa + cliente | ~200 |
| Views/Trips/Create.cshtml | UI criar | ~75 |
| Views/Trips/Edit.cshtml | UI editar | ~110 |
| Views/Trips/View.cshtml | UI visualizar | ~50 |
| Views/Home/Index.cshtml | Dashboard | ~45 |
| Program.cs | Startup | ~40 |

---

## 15. Stack Tecnológico

```
Backend:
  • .NET 10 / C#
  • ASP.NET Core (MVC + Controllers)
  • Clean Architecture (Domain, Application, Infrastructure)
  • In-Memory Repository (Thread-safe ConcurrentDictionary)

Frontend:
  • Razor Views (.cshtml)
  • Bootstrap 5.3.2 (CSS/UI)
  • Leaflet 1.9.4 (maps)
  • Leaflet Routing Machine 3.2.12 (routing OSRM)
  • SortableJS 1.15.0 (drag-drop)
  • Vanilla JavaScript

APIs externas:
  • OSRM público (https://router.project-osrm.org/route/v1)
  • OpenStreetMap Tiles

Data Formats:
  • JSON (API + DTO)
  • SVG (poster)
```

---

## 16. Próximos Passos Recomendados

### Fase 1: Persistência (Alta Prioridade)
- [ ] Instalar NuGet: EntityFrameworkCore, EntityFrameworkCore.Sqlite
- [ ] Criar DbContext com DbSet<Trip>, DbSet<Stop>, DbSet<RoutePoint>
- [ ] Migrar InMemoryTripRepository → EFTripRepository
- [ ] Testar CRUD com banco real

### Fase 2: Autenticação (Alta Prioridade)
- [ ] Adicionar Identity ou JWT
- [ ] Filtrar trips por usuário autenticado
- [ ] Controle de acesso (apenas dono pode editar)

### Fase 3: Export/Compartilhamento (Médio)
- [ ] Exportar poster como PNG/JPG (ImageSharp)
- [ ] Gerar versão quadrada (Instagram 1080x1080)
- [ ] Exportar em alta resolução (3000x2000)
- [ ] Compartilhamento via link público (read-only)

### Fase 4: Integrações (Médio)
- [ ] GPX export/import
- [ ] Integração Weather API (clima na data)
- [ ] Cálculo de combustível (fuel cost)
- [ ] Notificações de manutenção

### Fase 5: UX/UI (Médio)
- [ ] Ícones SVG customizados por categoria (não apenas letras)
- [ ] Busca de endereços (Geocoding)
- [ ] Dark/Light theme
- [ ] Modo offline (Service Worker)

### Fase 6: Cloud (Futuro)
- [ ] Deploy no Azure App Service
- [ ] Azure Blob Storage para posters
- [ ] Azure SQL Database
- [ ] CDN para assets

---

## 17. Instruções de Uso

### Criar uma Viagem
1. Acesse / (Dashboard)
2. Clique "+ Create Trip"
3. Preencha nome, descrição, datas
4. Clique no mapa para posicionar o centro
5. Clique "Add" para adicionar parada na posição central do mapa
6. Repita para cada parada
7. (Opcional) Clique "Calcular rota" para forçar recalcular
8. Clique "Salvar viagem"

### Editar uma Viagem
1. No Dashboard, clique "Edit" no card
2. Modifique campos/paradas
3. Clique "Salvar alterações"

### Visualizar e Gerar Poster
1. No Dashboard, clique "Open" no card
2. Veja mapa com rota
3. Clique "Generate Route Image"
4. Novo abrir em nova aba com poster SVG
5. Clique direito → "Salvar imagem como" para salvar SVG
6. Ou faça print/screenshot para PNG

### Duplicar Viagem
1. No Dashboard, clique "Duplicate" ou em /Trips/View/{id} clique "Duplicate"
2. Nova viagem é criada com mesmo nome + "(Copy)"

---

## 18. Debugging e Troubleshooting

### Roteiro não aparece no mapa
- Verificar: 2 ou mais stops foram criados?
- Console (F12): há erro de JavaScript?
- Network: POST a OSRM está retornando sucesso?
- Solução: Clique "Calcular rota" explicitamente

### Dados sumiram após reiniciar
- Esperado: repositório em memória
- Solução: implementar persistência (ver Fase 1)

### Poster sai com linhas retas
- Significado: RouteGeometry não foi salvo
- Verificação: GET /api/trips/{id} → verifique routeGeometry array
- Solução: Recalcule rota antes de salvar

### OSRM lento/offline
- Alternativa: Trocar URL em appsettings.json para outro roteador
- Fallback: Linhas retas entre stops (implementado)

---

## 19. Decisões de Design

| Decisão | Rationale | Trade-off |
|---------|-----------|-----------|
| **Em memória para MVP** | Prototipagem rápida | Perda de dados |
| **Leaflet + OSRM público** | Sem custo, código aberto | Dependência externa, limites de taxa |
| **Web Mercator para poster** | Fidelidade em áreas grandes | Complexidade matemática |
| **RDP com 8m tolerância** | Reduz pontos mantendo forma | Menos precisão em curvas cerradas |
| **SVG em lugar de PNG** | Vetorial, escalável, sem deps | Requer download; conversão PNG manual |
| **Categorias de stop fixas** | UX mais simples | Menos flexibilidade |
| **MVC (não Razor Pages)** | Controllers reutilizáveis para API | Mais arquivos |

---

## 20. Contato / Suporte

Para dúvidas técnicas ou melhorias, consulte:
- **GitHub**: https://github.com/adao-locks/Road-Route-Helper
- **Roadmap**: Implementação de persistência é critical path

---

**Data de Documentação**: 2025-01-XX  
**Versão**: 1.0 (MVP)  
**Status**: Funcional em staging; pronto para persistência e autenticação

