# Melhorias do Road Route Helper: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tornar a versão local confiável (bugs corrigidos e dados persistidos), com um fluxo de criação utilizável e exportação em GPX e PNG.

**Architecture:** O app continua em ASP.NET Core MVC. A lógica sai dos controllers e das views para classes testáveis (`TripMapper`, `PosterSvgBuilder`, `GpxBuilder`, `JsonTripRepository`), e o `trip-create.js` passa a ser o único dono do estado e do salvamento das telas de criação e edição.

**Tech Stack:** .NET 10, ASP.NET Core MVC, Razor, Leaflet 1.9.4, Leaflet Routing Machine 3.2.12, SortableJS, xUnit, WebApplicationFactory, Playwright para .NET, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-04-melhorias-road-route-helper-design.md`

## Global Constraints

- Ambiente somente local; sem deploy nem contratação de serviços. O app continua acessível só em `localhost`.
- Sem login, sem campos de dono e sem abstrações para vários usuários "para o futuro".
- Fora do escopo: importação de GPX, poster quadrado ou em outros tamanhos, clima, combustível, tema escuro, modo offline e sistema de tradução.
- Interface somente em português do Brasil a partir da Task 11; até lá, textos novos já entram em português.
- Texto vindo do usuário nunca é inserido no DOM como HTML. Números em SVG, GPX e JavaScript usam sempre cultura invariante.
- Sem biblioteca nova no servidor além das dependências de teste.
- TDD: nenhum código de produção sem um teste que falhe antes.
- Implementar em um branch próprio, nunca na `master`. Cada commit termina com a linha `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. Viagem com 0 ou 1 parada: salvar, abrir, gerar poster e GPX funcionam sem erro. Teste na Task 2 (poster), Task 3 (API) e Task 12 (GPX).
2. Nomes com acentos e caracteres especiais (`São João & <Cia>`): saem corretos no JSON, SVG, GPX, nome do arquivo e telas. Testes nas Tasks 2, 5, 7 e 12.
3. Paradas com coordenadas idênticas: o poster sai com números finitos e centralizado. Teste na Task 2.
4. Falha de gravação no disco: a API retorna erro, a memória não muda e o arquivo anterior fica intacto. Teste na Task 7.
5. Datas em fuso do Brasil: `2026-11-04` aparece como 04/11/2026 em todas as telas, sem voltar um dia. Teste na Task 11.

---

## Convenções de execução

- Testes rápidos: `dotnet test "Road Route Helper.slnx" --filter "Category!=E2E"`
- Testes de navegador: `dotnet test "Road Route Helper.slnx" --filter "Category=E2E"`
- Testes E2E carregam Bootstrap, Leaflet e SortableJS das CDNs atuais, então precisam de internet. Tiles e serviços de rota e geocodificação são sempre interceptados.

---

## Etapa 1: correção e segurança

### Task 1: Projeto de testes, CI e remoção do template

**Files:**
- Create: `Road Route Helper.Tests/Road Route Helper.Tests.csproj`
- Create: `Road Route Helper.Tests/Support/AppFactory.cs`, `E2EFixture.cs`, `OsrmStub.cs`, `Culture.cs`, `TripSamples.cs`
- Create: `Road Route Helper.Tests/SmokeTests.cs`
- Create: `.github/workflows/ci.yml`
- Modify: `Road Route Helper.slnx`, `Road Route Helper/Program.cs`, `Road Route Helper/Road Route Helper.http`
- Delete: `Road Route Helper/WeatherForecast.cs`, `Road Route Helper/Controllers/WeatherForecastController.cs`

**Interfaces:**
- Produces:
  - `AppFactory : WebApplicationFactory<Program>`; o construtor define `CultureInfo.DefaultThreadCurrentCulture = new("pt-BR")`.
  - `E2EFixture : IAsyncLifetime`, com `AppFactory Factory`, `string BaseUrl`, `HttpClient Api` e `Task<IPage> NewPageAsync()`. A página aborta `**/*.tile.openstreetmap.org/**` e guarda erros de script em `fixture.ScriptErrors(page)`. Sobe Kestrel com `factory.UseKestrel(0)` e `StartServer()`, recurso do .NET 10.
  - `OsrmStub.ReturnRouteAsync(IPage page, double distanceMeters, double durationSeconds)` e `OsrmStub.FailAsync(IPage page)`, interceptando `**/route/v1/**`. A resposta usa `{ code:"Ok", routes:[{ distance, duration, geometry:<polyline codificada>, legs:[{ steps:[], distance, duration, summary:"" }] }], waypoints:[...] }`, com um item em `waypoints` por coordenada pedida na URL.
  - `Culture.Use(string name) : IDisposable`, que troca e restaura `CurrentCulture`.
  - `TripSamples.Valid(string name = "Viagem", int stops = 2) : TripDto`, com paradas em torno de (-27.5, -48.5).

- [ ] **Step 1: Criar o projeto e referenciar o app**

```bash
dotnet new xunit -n "Road Route Helper.Tests" -f net10.0
dotnet add "Road Route Helper.Tests" reference "Road Route Helper/Road Route Helper.csproj"
dotnet add "Road Route Helper.Tests" package Microsoft.AspNetCore.Mvc.Testing --version 10.0.11
dotnet add "Road Route Helper.Tests" package Microsoft.Playwright
dotnet sln "Road Route Helper.slnx" add "Road Route Helper.Tests/Road Route Helper.Tests.csproj"
```

Adicionar `public partial class Program { }` ao final de `Program.cs`, remover `AddRazorPages` e `MapRazorPages`, e apagar o `UnitTest1.cs` gerado.

- [ ] **Step 2: Escrever o teste que falha**

```csharp
[Fact] public async Task WeatherForecast_foi_removido()
{
    using var f = new AppFactory();
    var r = await f.CreateClient().GetAsync("/WeatherForecast");
    Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
}
[Fact] public async Task Api_de_viagens_responde()
{
    using var f = new AppFactory();
    Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().GetAsync("/api/trips")).StatusCode);
}
[Fact, Trait("Category", "E2E")] public async Task Dashboard_abre_sem_erro_de_script() // via E2EFixture
```

- [ ] **Step 3: Rodar e confirmar a falha**

Run: `dotnet test "Road Route Helper.slnx" --filter "Category!=E2E"`
Expected: FAIL em `WeatherForecast_foi_removido` (retorna 200).

- [ ] **Step 4: Remover os arquivos do template e o `WeatherForecast` do `.http`**, deixando no `.http` um `GET /api/trips`.

- [ ] **Step 5: Instalar o Chromium e rodar tudo**

Run: `dotnet build "Road Route Helper.slnx"`, depois `powershell -File "Road Route Helper.Tests/bin/Debug/net10.0/playwright.ps1" install chromium`, depois `dotnet test "Road Route Helper.slnx"`
Expected: PASS.

- [ ] **Step 6: Criar `.github/workflows/ci.yml`**

Executa em `push` e `pull_request`, em `ubuntu-latest`: `actions/checkout@v4`, `actions/setup-dotnet@v4` com `dotnet-version: 10.0.x`, `dotnet build`, `pwsh "Road Route Helper.Tests/bin/Debug/net10.0/playwright.ps1" install --with-deps chromium` e `dotnet test`.

- [ ] **Step 7: Commit**: `test: adiciona projeto de testes e CI e remove template`

### Task 2: `PosterSvgBuilder` (bug 1 e casos-limite)

**Files:**
- Create: `Road Route Helper/Application/Posters/PosterSvgBuilder.cs`, `Road Route Helper/Application/Formatting/DurationFormatter.cs`
- Modify: `Road Route Helper/Controllers/TripsWebController.cs` (o método `PosterSvg` passa a só delegar)
- Test: `Road Route Helper.Tests/PosterSvgBuilderTests.cs`

**Interfaces:**
- Produces: `static string PosterSvgBuilder.Build(Trip trip)`; `static string DurationFormatter.Format(TimeSpan d)`, que retorna `"25h 00min"`.

- [ ] **Step 1: Escrever os testes que falham.** Analisar o SVG com `XDocument`. Um helper `Points(polyline)` faz `Split(' ')`, depois `Split(',')`, e exige exatamente 2 números invariantes por ponto.

```csharp
[Fact] void Em_ptBR_cada_ponto_da_linha_tem_x_e_y()            // using Culture.Use("pt-BR")
[Fact] void Em_ptBR_marcadores_ficam_dentro_do_canvas()         // 0<=cx<=1200, 0<=cy<=800, parse invariante
[Fact] void Marcador_coincide_com_o_ponto_da_rota()             // parada sobre o 1º ponto: diferença < 0.1
[Fact] void Viagem_sem_paradas_nem_tracado_gera_svg_vazio()     // Assert.Equal("<svg></svg>", svg)
[Fact] void Uma_parada_gera_svg_valido()
[Fact] void Tracado_de_um_ponto_e_tratado_como_ausente()        // usa as paradas na linha
[Fact] void Paradas_identicas_geram_numeros_finitos()           // sem "NaN" nem "Infinity"
[Fact] void Latitude_90_nao_gera_infinito()
[Fact] void Nome_com_caracteres_especiais_e_escapado()          // XDocument.Parse aceita; o título é "São João & <Cia>"
[Fact] void Duracao_de_25h_aparece_como_25h()                   // Assert.Contains("25h 00min", svg)
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test "Road Route Helper.slnx" --filter "FullyQualifiedName~PosterSvgBuilderTests"`
Expected: FAIL por `PosterSvgBuilder` não existir.

- [ ] **Step 3: Implementar `Build` movendo o código atual do controller.** Regras:
  - usar `StringBuilder` e `string.Create(CultureInfo.InvariantCulture, ...)` em toda interpolação numérica;
  - a projeção retorna `(double X, double Y)`, sem texto intermediário;
  - tratar geometria com menos de 2 pontos como ausente;
  - limitar a latitude a ±85.05 antes da projeção;
  - manter RDP com tolerância de 8 m;
  - remover os `try/catch` vazios;
  - escapar textos com `SecurityElement.Escape`.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `fix: gera poster correto em qualquer cultura`

### Task 3: API, mapeamento e validação (bugs 3, 6, 7 e 10 no servidor)

**Files:**
- Create: `Road Route Helper/Application/DTOs/TripMapper.cs`
- Modify: `Road Route Helper/Application/DTOs/TripDto.cs`, `Road Route Helper/Controllers/TripsController.cs`, `Road Route Helper/Domain/Trip.cs`, `Road Route Helper/Infrastructure/InMemoryTripRepository.cs`
- Test: `Road Route Helper.Tests/TripsApiTests.cs`

**Interfaces:**
- Produces:
  - `static TripDto TripMapper.ToDto(Trip)`, `static Trip TripMapper.FromDto(TripDto)` (movidos do controller)
  - `Trip Trip.DeepClone()`, que mantém os IDs
  - `Trip Trip.ToCopy()`, com novos IDs, sufixo `" (Copy)"` e todos os campos, inclusive `RouteGeometry`
- Regras em `TripDto`:
  - `[Required, StringLength(120, MinimumLength = 1)] Name`
  - `[Range(-90, 90)]` nas latitudes e `[Range(-180, 180)]` nas longitudes, inclusive em `StopDto` e `RoutePointDto`
  - `IValidatableObject` rejeita `EndDate < StartDate`, com a mensagem `"A data final deve ser igual ou posterior à inicial."`
  - `StopDto.Category` tem padrão `StopCategory.Custom`

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] Post_com_nome_vazio_retorna_400_com_erro_em_Name()
[Fact] Post_com_nome_de_121_caracteres_retorna_400()
[Fact] Post_com_latitude_999_na_parada_retorna_400()
[Fact] Post_com_data_final_anterior_retorna_400_com_a_mensagem()
[Fact] Post_com_data_final_igual_a_inicial_retorna_201()
[Fact] Post_sem_paradas_retorna_201()
[Fact] Post_com_uma_parada_retorna_201()
[Fact] Parada_sem_categoria_vira_Custom()                  // Assert.Equal(StopCategory.Custom, ...)
[Fact] Duracao_de_um_dia_e_uma_hora_e_aceita()             // "1.01:00:00" retorna 201 e volta TimeSpan de 25h
[Fact] Duplicar_mantem_o_tracado()                         // mesma contagem e mesmos pontos
[Fact] Put_mantem_todos_os_campos_enviados()               // ida e volta completa do DTO
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL nos testes de validação, de categoria e de duplicação.

- [ ] **Step 3: Implementar as regras e o mapper**, sem mudar as rotas da API. `DuplicateAsync` passa a usar `ToCopy()`.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `fix: valida a API e preserva dados ao duplicar`

### Task 4: Salvamento único no front-end (bugs 2, 3, 6 e 10 na tela)

**Files:**
- Modify: `Road Route Helper/wwwroot/js/trip-create.js`, `Road Route Helper/Views/Trips/Edit.cshtml`, `Road Route Helper/Views/Trips/Create.cshtml`, `Road Route Helper/Views/Trips/View.cshtml`
- Test: `Road Route Helper.Tests/E2E/TripEditorTests.cs`

**Interfaces:**
- Consumes: `TripMapper.ToDto` (Task 3), `DurationFormatter.Format` (Task 2).
- Produces:
  - O `Edit.cshtml` emite `<script type="application/json" id="trip-initial-data">`, com `TripMapper.ToDto(Model)` serializado por `JsonSerializerDefaults.Web`. Nenhum valor do servidor é interpolado em JavaScript.
  - `window._tripCreate.trip` é o DTO completo em memória; `saveTrip` envia `{ ...trip, campos do formulário, stops, route }`.
  - Há ID: `PUT /api/trips/{id}`; sem ID: `POST /api/trips`.
  - `formatDuration(seconds)` retorna `hh:mm:ss`, ou `d.hh:mm:ss` a partir de 24h.
  - `<div id="saveError" role="alert">` mostra erros de `ValidationProblemDetails`, sem `alert()`.

- [ ] **Step 1: Escrever os testes E2E que falham** (`[Trait("Category","E2E")]`)

```csharp
Edicao_com_distancia_decimal_abre_sem_erro_de_script()    // TotalDistanceKm=123.45; ScriptErrors vazio
Salvar_edicao_nao_cria_outra_viagem()                     // contagem em GET /api/trips não muda
Editar_e_salvar_mantem_categoria_e_notas()                // Hotel e "nota" continuam após salvar
Rota_de_25h_e_salva()                                     // OsrmStub 90000 s; API devolve 25h
Tela_de_visualizacao_mostra_25h()                         // View.cshtml usa DurationFormatter: "25h 00min"
Nome_vazio_mostra_erro_na_tela()                          // #saveError visível; continua na página
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test "Road Route Helper.slnx" --filter "FullyQualifiedName~TripEditorTests"`
Expected: FAIL, incluindo `SyntaxError` em `Edicao_com_distancia_decimal...`.

- [ ] **Step 3: Implementar.** Remover o script inline de `Edit.cshtml` e passar a usar a estrutura acima; trocar `window._lastRoute` por `state.route = { distanceKm, duration, geometry }`.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `fix: centraliza o salvamento de viagens no trip-create`

### Task 5: XSS e reordenação (bugs 4 e 5)

**Files:**
- Create: `Road Route Helper/wwwroot/js/dom.js`
- Modify: `Road Route Helper/wwwroot/js/trip-create.js`, `Road Route Helper/Views/Home/Index.cshtml`, `Road Route Helper/Views/Trips/View.cshtml`, `Road Route Helper/Views/Shared/_Layout.cshtml`
- Test: `Road Route Helper.Tests/E2E/SafetyAndReorderTests.cs`

**Interfaces:**
- Produces: `window.textPopup(text) : HTMLElement`, um `div` preenchido com `textContent`, para usar em todo `bindPopup`.

- [ ] **Step 1: Escrever os testes que falham.** O payload é `<img src=x onerror="window.__xss=1">`; o teste avalia `window.__xss` depois de 500 ms.

```csharp
Dashboard_nao_executa_script_no_nome()              // e o texto aparece literal no card
Lista_de_paradas_nao_executa_script_no_nome()       // tela de edição
Popup_do_mapa_nao_executa_script_no_nome()          // tela de visualização, abrindo o popup
Nome_acentuado_aparece_correto_no_dashboard()       // "São João & <Cia>"
Reordenar_paradas_de_mesmo_nome_mantem_as_tres()    // ids distintos == 3 e ordem nova
```

Para reordenar, mover o `li` no DOM e chamar `Sortable.get(ul).options.onEnd({})`, o que evita instabilidade do arraste simulado.

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL, com `window.__xss == 1` e menos de 3 IDs distintos.

- [ ] **Step 3: Implementar.** Montar cards e itens de lista com `createElement`/`textContent`, usar `addEventListener` no lugar de `onclick` montado em texto, colocar `data-id` em cada `li`, reordenar por esse ID e criar o Sortable só uma vez.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `fix: impede XSS e corrige a reordenação de paradas`

### Task 6: Estado da rota e ações mortas (bugs 8 e 9 e itens 3.3)

**Files:**
- Modify: `Road Route Helper/wwwroot/js/trip-create.js`, `Road Route Helper/Views/Trips/View.cshtml`, `Road Route Helper/Views/Trips/Create.cshtml`, `Road Route Helper/Views/Trips/Edit.cshtml`, `Road Route Helper/Views/Shared/_Layout.cshtml`, `Road Route Helper/Controllers/HomeController.cs`, `Road Route Helper/Controllers/TripsWebController.cs`, `Road Route Helper/appsettings.json`
- Test: `Road Route Helper.Tests/E2E/RouteStateTests.cs`, `Road Route Helper.Tests/NavigationTests.cs`

**Interfaces:**
- Produces:
  - `calculateRoute() : Promise<boolean>`, resolvida por `routesfound` (`true`) ou `routingerror` (`false`), sem polling.
  - `<div id="routeStatus" role="status">` para mensagens de rota.
  - `Routing:ServiceUrl` passa a ser obrigatório em `appsettings.json`, com o valor `https://router.project-osrm.org/route/v1`, e fica sem fallback em controllers e views.
  - `state.savedRouteLine`: a linha carregada ao abrir a edição.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// E2E
Mudar_paradas_invalida_a_rota_anterior()           // ao remover parada, route == null até nova resposta
Calcular_rota_espera_o_resultado_novo()            // resposta atrasada: a promessa só resolve depois dela
Falha_no_servico_mostra_mensagem_e_permite_salvar() // OsrmStub.FailAsync; viagem salva com geometria vazia
Linha_antiga_sai_do_mapa_quando_a_rota_muda()      // savedRouteLine fora do mapa
Tela_de_visualizacao_mostra_atribuicao_OSM()
Botao_duplicar_cria_copia_e_abre_a_pagina()        // URL muda para o novo ID
Tela_de_criacao_nao_tem_botao_de_poster()
// Integração
[Fact] Links_do_menu_nao_retornam_404()            // extrai hrefs da navbar
[Fact] Home_Create_foi_removido()                  // 404
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL nos testes acima.

- [ ] **Step 3: Implementar.** O botão "Duplicar" faz POST em `/api/trips/{id}/duplicate` e navega para `/Trips/View/{novoId}`. Remover o item "Map" do menu, o card "Generate" de `Create.cshtml` e `HomeController.Create`.

- [ ] **Step 4: Rodar a suíte inteira**

Run: `dotnet test "Road Route Helper.slnx"`
Expected: PASS, com `dotnet build` sem o aviso CS8600.

- [ ] **Step 5: Commit**: `fix: corrige o estado da rota e remove ações sem função`

---

## Etapa 2: persistência em JSON

### Task 7: `JsonTripRepository`

**Files:**
- Create: `Road Route Helper/Infrastructure/JsonTripRepository.cs`, `Road Route Helper/Infrastructure/TripStoreException.cs`
- Modify: `Road Route Helper/Program.cs`, `Road Route Helper/appsettings.json`, `.gitignore`, `Road Route Helper.Tests/Support/AppFactory.cs`, testes que dependiam da viagem de exemplo
- Delete: `Road Route Helper/Infrastructure/InMemoryTripRepository.cs`
- Test: `Road Route Helper.Tests/JsonTripRepositoryTests.cs`, `Road Route Helper.Tests/PersistenceStartupTests.cs`

**Interfaces:**
- Consumes: `ITripRepository`, `Trip.DeepClone()`, `Trip.ToCopy()`.
- Produces:
  - `JsonTripRepository(string filePath)`; o construtor lê o arquivo e lança `TripStoreException` se estiver corrompido ou com `schemaVersion` diferente de 1.
  - Configuração `Storage:TripsFilePath = "App_Data/trips.json"`, relativa a `ContentRootPath`.
  - O `Program.cs` resolve `ITripRepository` antes de `app.Run()`, para falhar já na inicialização.
  - `AppFactory` passa a apontar `Storage:TripsFilePath` para um arquivo temporário exclusivo e o apaga no `Dispose`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Dados_sobrevivem_a_nova_instancia()
Arquivo_ausente_inicia_vazio()                       // GetAllAsync vazio e pasta criada ao gravar
Todas_as_operacoes_funcionam()                       // create, get, update, delete, duplicate
Arquivo_usa_schemaVersion_1()                        // JSON raiz tem schemaVersion == 1 e trips
Alterar_objeto_retornado_nao_altera_o_repositorio()
Gravacao_mantem_bak_da_versao_anterior()
Arquivo_corrompido_gera_erro_e_fica_intacto()        // bytes idênticos após a exceção
Versao_desconhecida_gera_erro_e_fica_intacta()
Cinquenta_gravacoes_simultaneas_nao_perdem_viagens() // Task.WhenAll; nova instância lê 50
Nome_acentuado_sobrevive_a_recarga()                 // "São João & <Cia>"
Falha_de_gravacao_nao_altera_memoria_nem_arquivo()   // arquivo bloqueado com FileShare.None; exceção propaga, GetAll e bytes inalterados
// Integração
App_nao_inicia_com_arquivo_corrompido()              // factory.CreateClient lança; arquivo intacto
Viagem_criada_pela_API_sobrevive_a_novo_app()        // duas AppFactory com o mesmo arquivo
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL por `JsonTripRepository` não existir.

- [ ] **Step 3: Implementar.**
  - Estado em `Dictionary<Guid, Trip>`, protegido por `SemaphoreSlim(1,1)`.
  - Cada alteração monta o novo estado, grava `trips.json.tmp` na mesma pasta e chama `File.Replace(tmp, arquivo, arquivo + ".bak")`. Na primeira gravação, usa `File.Move`.
  - A memória só é atualizada depois de a gravação concluir.
  - Tudo que entra ou sai passa por `DeepClone()`.

- [ ] **Step 4: Registrar no `Program.cs`, remover o repositório em memória e a viagem de exemplo, e adicionar `App_Data/` ao `.gitignore`.**

- [ ] **Step 5: Rodar a suíte inteira**: Expected: PASS.

- [ ] **Step 6: Commit**: `feat: persiste viagens em arquivo JSON`

---

## Etapa 3: fluxo de criação

### Task 8: Categorias únicas e remoção de início e destino

**Files:**
- Create: `Road Route Helper/Domain/StopCategoryStyles.cs`
- Modify: `Road Route Helper/Application/Posters/PosterSvgBuilder.cs`, `Road Route Helper/Domain/Trip.cs`, `Road Route Helper/Application/DTOs/TripDto.cs`, `Road Route Helper/Application/DTOs/TripMapper.cs`, `Road Route Helper/Views/Trips/View.cshtml`
- Test: `Road Route Helper.Tests/StopCategoryStylesTests.cs`, `Road Route Helper.Tests/TripsApiTests.cs`, `Road Route Helper.Tests/PosterSvgBuilderTests.cs`

**Interfaces:**
- Produces: `record StopCategoryStyle(StopCategory Category, string Label, string Letter, string Color)`; `static IReadOnlyList<StopCategoryStyle> StopCategoryStyles.All`; `static StopCategoryStyle StopCategoryStyles.For(StopCategory)`.

Valores, com letras e cores iguais às atuais do poster:

| Categoria | Label | Letra | Cor |
|---|---|---|---|
| Restaurant | Restaurante | R | `#ef4444` |
| PhotoSpot | Ponto de foto | P | `#f59e0b` |
| GasStation | Posto de combustível | G | `#f97316` |
| Hotel | Hotel | H | `#06b6d4` |
| TouristAttraction | Atração turística | T | `#8b5cf6` |
| CoffeeShop | Café | C | `#a16207` |
| MotorcycleStop | Parada de moto | M | `#10b981` |
| Maintenance | Manutenção | ! | `#ef4444` |
| Custom | Outro | • | `#6b7280` |

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] Todas_as_categorias_tem_estilo()                    // Enum.GetValues: For(c) não lança; All.Count == 9
[Fact] Poster_usa_o_estilo_da_categoria()                  // Hotel: fill #06b6d4 e letra H
[Fact] Dto_nao_expoe_inicio_nem_destino()                  // reflexão: nenhuma propriedade começa com Starting ou Destination
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL.

- [ ] **Step 3: Implementar.** Criar a definição, usá-la no poster e remover os seis campos de `Trip`, `TripDto` e `TripMapper`. Em `View.cshtml`, o mapa passa a iniciar pela primeira parada. O JSON antigo continua legível, porque propriedades desconhecidas são ignoradas.

- [ ] **Step 4: Rodar a suíte inteira**: Expected: PASS, ajustando `Put_mantem_todos_os_campos_enviados` para os campos restantes.

- [ ] **Step 5: Commit**: `refactor: centraliza categorias e remove início e destino`

### Task 9: Paradas no mapa

**Files:**
- Modify: `Road Route Helper/wwwroot/js/trip-create.js`, `Road Route Helper/Views/Trips/Create.cshtml`, `Road Route Helper/Views/Trips/Edit.cshtml`, `Road Route Helper/Views/Trips/View.cshtml`, `Road Route Helper/wwwroot/css/site.css`
- Test: `Road Route Helper.Tests/E2E/StopEditingTests.cs`

**Interfaces:**
- Consumes: `StopCategoryStyles.All`, emitido nas telas em `<script type="application/json" id="stop-categories">`.
- Produces:
  - `openStopForm(latlng, stop?)`: formulário em popup, com `#stopFormName`, `#stopFormCategory`, `#stopFormNotes` e `#stopFormSave`.
  - `categoryIcon(category) : L.DivIcon`.
  - `scheduleRouting()`: debounce de 500 ms; todas as mudanças de paradas passam por ela.
  - Cada `li` tem um botão "Editar", que abre `openStopForm` no ponto da parada.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Clicar_no_mapa_e_salvar_cria_parada_com_os_dados()    // nome, categoria Hotel, notas, lat/lng do clique
Criar_parada_nao_tem_mais_botao_no_centro()           // #stopName não existe
Editar_na_lista_altera_e_persiste_ao_salvar()
Arrastar_marcador_altera_posicao_e_recalcula()        // marker.setLatLng + fire('dragend'); nova chamada ao stub
Categoria_escolhida_chega_ao_poster()                 // SVG com a cor da categoria
Varias_mudancas_rapidas_geram_uma_chamada_de_rota()   // 3 mudanças em menos de 500 ms = 1 requisição
Nome_em_branco_mostra_erro_no_formulario()
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL.

- [ ] **Step 3: Implementar.** Os marcadores usam `draggable: true` e `categoryIcon`; o Routing Machine recebe `createMarker: () => null`, para não duplicar marcadores. A tela de visualização usa o mesmo ícone, sem arraste. Textos do usuário continuam em `textContent`/`value`.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `feat: cria e edita paradas diretamente no mapa`

### Task 10: Busca de endereços

**Files:**
- Create: `Road Route Helper/wwwroot/js/geocoding.js`
- Modify: `Road Route Helper/Views/Trips/Create.cshtml`, `Road Route Helper/Views/Trips/Edit.cshtml`, `Road Route Helper/Controllers/TripsWebController.cs`, `Road Route Helper/appsettings.json`
- Test: `Road Route Helper.Tests/E2E/GeocodingTests.cs`

**Interfaces:**
- Consumes: `openStopForm(latlng)` (Task 9).
- Produces:
  - `Geocoding:ServiceUrl = "https://nominatim.openstreetmap.org/search"`
  - `<form id="addressSearch">` com `#addressQuery`, `#addressResults` e `#addressStatus`, além da atribuição "Busca por © OpenStreetMap"
  - Requisição: `?format=jsonv2&limit=5&q=<texto>`

- [ ] **Step 1: Escrever os testes que falham**, interceptando `**/search?*`

```csharp
Escolher_resultado_centraliza_e_abre_formulario()
Busca_so_roda_ao_enviar()                             // digitar não gera requisição
Duas_buscas_seguidas_respeitam_um_segundo()           // botão desabilitado; intervalo >= 1000 ms
Sem_resultados_mostra_mensagem()
Erro_do_servico_mostra_mensagem_e_mapa_segue_usavel() // clique no mapa ainda abre o formulário
Nome_de_resultado_com_html_aparece_como_texto()
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL.

- [ ] **Step 3: Implementar `geocoding.js`**, usando `fetch`, `encodeURIComponent`, `textContent` e o `submit` do formulário.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `feat: adiciona busca de endereços`

### Task 11: Interface em português e mapa inicial

**Files:**
- Modify: `Road Route Helper/Views/**/*.cshtml`, `Road Route Helper/wwwroot/js/trip-create.js`, `Road Route Helper/Domain/Trip.cs` (sufixo `" (Cópia)"`), `Road Route Helper/Application/Formatting/DurationFormatter.cs` se necessário
- Test: `Road Route Helper.Tests/E2E/LocalizationTests.cs`, `Road Route Helper.Tests/TripsApiTests.cs`

**Interfaces:**
- Produces: `toDateInput`/`formatDate` em `dom.js`. `formatDate("2026-11-04T00:00:00-03:00")` retorna `04/11/2026`, usando a parte de data do valor recebido, sem conversão de fuso.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Paginas_tem_lang_ptBR()
Paginas_nao_tem_textos_antigos_em_ingles()   // Dashboard, Create Trip, Edit, Delete, Open, Stops, Back, Duplicate, Details
Data_nao_volta_um_dia_no_fuso_do_Brasil()    // contexto com TimezoneId "America/Sao_Paulo"; Dashboard mostra 04/11/2026
Data_e_igual_apos_editar_e_salvar()          // input continua 2026-11-04
Distancia_usa_formato_brasileiro()           // tela de visualização mostra "123,5 km"
Mapa_novo_abre_centralizado_no_Brasil()      // centro em (-14.2350, -51.9253), zoom 4
[Fact] Duplicar_usa_sufixo_Copia()           // " (Cópia)"
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL.

- [ ] **Step 3: Traduzir textos, títulos, mensagens e `lang="pt-BR"`**, e definir o centro do mapa como `[-14.2350, -51.9253]`, zoom 4. A API continua com nomes em inglês.

- [ ] **Step 4: Rodar a suíte inteira**: Expected: PASS.

- [ ] **Step 5: Commit**: `feat: padroniza a interface em português`

---

## Etapa 4: exportação

### Task 12: Exportar GPX

**Files:**
- Create: `Road Route Helper/Application/Exports/GpxBuilder.cs`, `Road Route Helper/Application/Formatting/FileNames.cs`
- Create: `Road Route Helper.Tests/TestData/gpx11.xsd`, copiado de `https://www.topografix.com/GPX/1/1/gpx.xsd` e configurado para ir à saída dos testes
- Modify: `Road Route Helper/Controllers/TripsWebController.cs`, `Road Route Helper/Views/Trips/View.cshtml`
- Test: `Road Route Helper.Tests/GpxBuilderTests.cs`

**Interfaces:**
- Produces:
  - `static string GpxBuilder.Build(Trip trip)`
  - `static string FileNames.Slug(string name)`: remove acentos, usa minúsculas e hifens e retorna `"viagem"` se ficar vazio
  - `GET /Trips/View/{id}/route.gpx`, com `application/gpx+xml` e arquivo `<slug>.gpx`
  - Link `#exportGpx` na tela da viagem

- [ ] **Step 1: Escrever os testes que falham**

```csharp
[Fact] Gpx_e_valido_no_schema_1_1()                 // XmlSchemaSet com gpx11.xsd, sem erros
[Fact] Em_ptBR_coordenadas_usam_ponto()             // lat="-27.5", nunca vírgula
[Fact] Paradas_viram_wpt_na_ordem_da_viagem()
[Fact] Tracado_vira_trk()                           // trkpt em quantidade igual à geometria
[Fact] Sem_tracado_nao_gera_trk()
[Fact] Sem_paradas_gera_gpx_valido()
[Fact] Nome_especial_e_escapado()                   // XDocument lê "São João & <Cia>"
[Fact] Slug_remove_acentos()                        // "São João & <Cia>" => "sao-joao-cia"
[Fact] Endpoint_retorna_anexo_e_404_para_id_inexistente()
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL.

- [ ] **Step 3: Implementar com `XDocument`** e o namespace `http://www.topografix.com/GPX/1/1`, com `version="1.1"` e `creator="Road Route Helper"`. Coordenadas com `ToString("0.######", CultureInfo.InvariantCulture)`; `wpt` antes de `trk`, como o schema exige.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `feat: exporta viagens em GPX`

### Task 13: Exportar poster em PNG

**Files:**
- Create: `Road Route Helper/wwwroot/js/poster-png.js`
- Modify: `Road Route Helper/Views/Trips/View.cshtml`
- Test: `Road Route Helper.Tests/E2E/PosterPngTests.cs`

**Interfaces:**
- Produces: `exportPosterPng(svgUrl, fileName) : Promise<void>`; botão `#exportPng` e área `#exportError`, com `role="alert"`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Botao_baixa_png_de_2400_por_1600()  // download: assinatura PNG; IHDR com largura 2400 e altura 1600 (big-endian)
Nome_do_arquivo_usa_o_slug()        // "sao-joao-cia.png"
Falha_ao_buscar_svg_mostra_erro()   // rota interceptada com 500; #exportError visível
```

- [ ] **Step 2: Rodar e confirmar a falha**: Expected: FAIL.

- [ ] **Step 3: Implementar.** Buscar o SVG com `fetch`, criar `Blob` e `Image`, desenhar em um canvas de 2400×1600 e baixar com `canvas.toBlob`. O slug vem do servidor em `data-file-name`, usando `FileNames.Slug`.

- [ ] **Step 4: Rodar os testes**: Expected: PASS.

- [ ] **Step 5: Commit**: `feat: exporta o poster em PNG`

---

## Encerramento

### Task 14: Documentação e verificação final

**Files:**
- Modify: `README.md`, `Road Route Helper/PROJECT_SUMMARY.md`

- [ ] **Step 1: Atualizar o `README.md`** com: o que é o app, pré-requisitos (.NET 10 e internet para CDNs, mapas e rotas), `dotnet run --project "Road Route Helper/Road Route Helper.csproj"`, comandos de teste, instalação do Chromium, local do `trips.json` e o aviso: **não publicar antes de concluir a etapa 5 do spec**.

- [ ] **Step 2: Reescrever o `PROJECT_SUMMARY.md`** para refletir o código final, removendo o que não existe: gradiente na rota do mapa, duplicar no Dashboard, repositório em memória, início e destino, além das seções 16 e 19 desatualizadas. Colocar um link para o spec no lugar do roadmap.

- [ ] **Step 3: Rodar a verificação completa**

Run: `dotnet build "Road Route Helper.slnx" -warnaserror`, depois `dotnet test "Road Route Helper.slnx"`
Expected: 0 avisos; todos os testes passam.

- [ ] **Step 4: Fazer o roteiro manual no app real**, com internet e os serviços de verdade: criar uma viagem de 3 paradas com busca e clique, reordenar, salvar, reiniciar o app, conferir que continua lá, editar, duplicar, baixar SVG, PNG e GPX e abrir cada um. Expected: todos corretos, com geometria seguindo as estradas. Registrar qualquer divergência antes de concluir.

- [ ] **Step 5: Commit**: `docs: atualiza README e resumo do projeto`
