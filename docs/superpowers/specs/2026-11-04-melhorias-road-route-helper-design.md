# Melhorias do Road Route Helper: desenho

**Data:** 2026-11-04
**Status:** aguardando revisão

## 1. Objetivo e contexto

O Road Route Helper é um app ASP.NET Core 10 para planejar viagens de moto, com paradas, rota no mapa e poster da viagem. A meta de longo prazo é um **produto público com contas**, com viagens privadas e link público somente leitura.

Por enquanto o app roda **só localmente**. Este trabalho deixa a versão local confiável e útil, sem antecipar a infraestrutura de produção.

### Decisões tomadas

| Tema | Decisão |
|---|---|
| Meta do produto | Público, com contas, viagens privadas e link somente leitura |
| Ambiente atual | Somente local; sem deploy nem contratação de serviços |
| Armazenamento atual | Arquivo JSON, como solução temporária |
| Login e link público | Adiados até existir banco real (etapa 5) |
| Idioma | Interface somente em português do Brasil |

### Critério de conclusão (etapas 1 a 4)

- Os testes passam no CI, incluindo os executados com cultura pt-BR.
- É possível criar, editar, reordenar e duplicar uma viagem, e tudo continua lá depois de reiniciar o app.
- O poster e o GPX saem corretos para uma viagem real.
- A etapa 5 continua registrada como condição obrigatória para publicar.

## 2. Escopo

| Etapa | Conteúdo | Quando |
|---|---|---|
| 1. Correção e segurança | Testes, CI, correção de bugs e limpeza | Agora |
| 2. Persistência em JSON | As viagens sobrevivem ao reiniciar o app | Agora |
| 3. Fluxo de criação | Clique no mapa, categorias, edição de paradas, busca de endereços e idioma único | Agora |
| 4. Exportação | GPX e poster em PNG | Agora |
| 5. Preparação para o público | Banco real, contas, dono por viagem, link público, deploy e servidor de rotas próprio | Antes de publicar, com spec próprio |

### Fora do escopo das etapas 1 a 4

- Abstrações para vários usuários ou campos de dono "para o futuro".
- Importação de GPX.
- Versão quadrada do poster e tamanhos adicionais.
- Clima, cálculo de combustível, tema escuro e modo offline.
- Sistema de tradução.

## 3. Etapa 1: correção e segurança

Cada bug é corrigido na causa, começando por um teste que falha e prova o problema.

### 3.1 Testes e CI

- **Projeto:** novo `Road Route Helper.Tests`, adicionado à solução, com xUnit.
- **Integração:** `WebApplicationFactory` para testar a API e as páginas de verdade.
- **Navegador:** Playwright para .NET, com poucos testes dos fluxos principais e o serviço de rotas simulado, para não depender da internet.
- **CI:** GitHub Actions executa build e testes a cada push e pull request. Os testes de formatação forçam a cultura pt-BR no próprio teste, independentemente da máquina.

### 3.2 Bugs confirmados na execução

| # | Problema | Correção | Teste de aceitação |
|---|---|---|---|
| 1 | O poster sai errado em pt-BR, porque números usam vírgula decimal | Extrair o gerador para uma classe `PosterSvgBuilder`, sem estado e sem dependência do controller, com números sempre em cultura invariante e sem `try/catch` vazios | Com cultura pt-BR, as coordenadas da linha e dos marcadores saem corretas |
| 2 | Com distância decimal, a página de edição quebra e "Salvar" cria outra viagem | Remover o `saveTrip` do `Edit.cshtml`; o `trip-create.js` é o único a salvar e usa PUT quando a viagem tem ID. Valores do servidor chegam à página serializados em JSON | Com distância decimal, a página abre sem erro de script e salvar não aumenta o número de viagens |
| 3 | Editar apaga categoria, notas e outros campos | O front-end guarda e reenvia todos os campos; parada sem categoria vira `Custom`, não `Restaurant` | Editar e salvar mantém os campos da viagem e das paradas |
| 4 | XSS armazenado em nomes de viagem e parada | Inserir textos com `textContent` no Dashboard, na lista de paradas e nos popups do mapa | Um script injetado no nome não executa em nenhuma dessas telas |
| 5 | Reordenar paradas de mesmo nome as transforma em cópias | Identificar cada item por `data-id` e criar o Sortable uma única vez | Três paradas de mesmo nome continuam distintas depois de reordenar |
| 6 | Viagens com mais de 24h não salvam | Enviar a duração em `d.hh:mm:ss` e corrigir sua exibição no poster | Uma viagem de 25h salva e é exibida como 25h |
| 7 | Duplicar perde o traçado | Copiar também o `RouteGeometry` | A cópia tem os mesmos pontos do original |
| 8 | O botão "Duplicate" faz GET em um endpoint só de POST | Trocar por POST e abrir a cópia criada | O clique cria a cópia e abre sua página |
| 9 | Links e botões mortos | Remover o botão de poster da tela de criação, o menu "Map" e o `HomeController.Create` | Nenhum link do menu retorna 404 e a tela de criação não tem botão sem função |
| 10 | A API não valida os dados | Exigir nome com 1 a 120 caracteres, latitude entre -90 e 90, longitude entre -180 e 180 e data final igual ou posterior à inicial; mostrar os erros na tela | Dados inválidos retornam 400 com mensagem clara |

### 3.3 Problemas vistos só no código

Cada um é primeiro reproduzido por um teste.

- **Rota antiga salva por engano:** qualquer mudança nas paradas invalida o traçado anterior, e "Calcular rota" espera o resultado novo.
- **Falha no serviço de rotas:** o evento `routingerror` mostra uma mensagem na tela. A viagem ainda pode ser salva sem traçado, com um aviso claro.
- **Linha antiga no mapa:** é removida quando a rota muda.
- **Atribuição do OpenStreetMap:** é adicionada à tela de visualização.
- **Casos-limite do poster:** traçado com menos de 2 pontos é tratado como ausente, e a latitude é limitada a ±85,05 na projeção, para evitar valores infinitos.

### 3.4 Limpeza

- Remover `WeatherForecast`, seu controller, a chamada correspondente no `.http` e o `AddRazorPages` sem uso.
- Eliminar o aviso de nulidade em `TripsWebController`.
- Centralizar a URL do serviço de rotas em `Routing:ServiceUrl`, no `appsettings.json`.
- Atualizar o `PROJECT_SUMMARY.md` para refletir o que existe e colocar no `README.md` como rodar o app e os testes.

Melhorias visuais e o seletor de categoria ficam para a etapa 3.

## 4. Etapa 2: persistência em JSON

Uma nova classe `JsonTripRepository` implementa o `ITripRepository` atual, então controllers e telas não mudam. Migrar para um banco significa criar outra implementação dessa interface.

| Aspecto | Comportamento |
|---|---|
| Local do arquivo | `Storage:TripsFilePath`, com padrão `App_Data/trips.json`, relativo à raiz do app; a pasta é criada se faltar e fica no `.gitignore` |
| Formato | `{ "schemaVersion": 1, "trips": [...] }` |
| Arquivo ausente | O app inicia sem viagens |
| Carregamento | Na inicialização do app, mantendo os dados em memória |
| Gravação | Cada alteração grava um arquivo temporário e substitui o original, mantendo um `.bak` |
| Concorrência | Um `SemaphoreSlim` serializa as alterações dentro do processo |
| Isolamento | O repositório devolve e recebe cópias, para que nada altere os dados sem gravá-los |
| Arquivo corrompido ou versão desconhecida | O app não inicia, mostra um erro claro e não sobrescreve o arquivo |
| Viagem de exemplo | Sai do código e fica só nos testes |

O `InMemoryTripRepository` é removido quando o novo repositório estiver em uso.

**Testes:** os dados sobrevivem a uma nova instância do repositório; todas as operações do `ITripRepository` funcionam; um arquivo corrompido gera erro e permanece intacto; gravações simultâneas não perdem alterações.

### Limites aceitos

- O arquivo inteiro é regravado a cada alteração, e só um processo do app pode usá-lo.
- Sem login, quem acessa o app vê e altera todas as viagens. Por isso ele continua acessível só em `localhost`.
- **Publicar exige concluir a etapa 5 antes.**

## 5. Etapa 3: fluxo de criação

| Melhoria | Comportamento |
|---|---|
| Adicionar parada | Clicar no mapa abre um formulário no ponto clicado, com nome, categoria e notas; o botão que cria a parada no centro do mapa sai |
| Editar parada | Na lista é possível alterar nome, categoria e notas; arrastar o marcador muda a posição e recalcula a rota |
| Categorias | Seletor com as 9 categorias atuais; cores e letras ficam em uma única definição no servidor, usada pelo poster e enviada às telas do mapa |
| Busca de endereços | Campo com botão de buscar, chamando o Nominatim pelo navegador, com URL em `Geocoding:ServiceUrl`. A busca roda só ao enviar, no máximo uma por segundo, e exibe a atribuição. Escolher um resultado centraliza o mapa e abre o formulário da parada |
| Recálculo da rota | Aguarda 500 ms depois da última mudança nas paradas |
| Idioma | Toda a interface em português do Brasil, com datas e distâncias nesse formato |
| Mapa inicial | Centralizado no Brasil, em vez de Los Angeles |

### Início e destino

A primeira parada é o início e a última é o destino. Os campos `StartingPointName`, `StartingLatitude`, `StartingLongitude`, `DestinationName`, `DestinationLatitude` e `DestinationLongitude` saem de `Trip` e `TripDto`, porque nenhuma tela permite preenchê-los. Nessa etapa, o teste do bug 3 deixa de cobrir esses campos.

**Testes:** clique no mapa cria parada com os dados informados; edição e arraste persistem ao salvar; a categoria escolhida chega ao poster; a busca funciona com o serviço simulado, inclusive sem resultados e com erro.

## 6. Etapa 4: exportação

### GPX

- Um botão na tela da viagem baixa o arquivo por `GET /Trips/View/{id}/route.gpx`.
- As paradas viram `wpt`, na ordem da viagem, e o traçado vira `trk`. Sem traçado salvo, o arquivo traz só as paradas.
- **Testes:** o XML é válido em GPX 1.1, os números saem com ponto decimal em pt-BR e nomes com caracteres especiais saem corretos.

### Poster em PNG

- Um botão na tela da viagem converte o SVG em imagem no navegador, em 2400×1600, sem biblioteca nova no servidor.
- O visual é igual ao do SVG, e as fontes dependem do sistema de quem exporta.
- **Teste:** o Playwright confirma que o download é um PNG com as dimensões esperadas.

## 7. Etapa 5: condição para publicar

Esta etapa terá spec próprio e deve estar concluída antes de qualquer publicação:

- Banco real e importação do `trips.json`, usando o `schemaVersion`.
- Contas, dono por viagem e controle de acesso.
- Recuperação de senha, que exige um serviço de e-mail.
- Link público somente leitura.
- Deploy e backups.
- Servidores de rotas e de geocodificação adequados à produção, porque os serviços públicos do OSRM e do Nominatim têm limites de uso.
