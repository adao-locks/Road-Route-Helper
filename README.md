# Road Route Helper

Aplicação web para planejar viagens de moto: crie paradas no mapa, calcule a rota e exporte o resultado em poster (SVG e PNG) ou GPX.

> **Não publique este app ainda.** Ele não tem login, e qualquer pessoa que o acesse vê e altera todas as viagens. As viagens ficam em um arquivo JSON, adequado apenas para uso local. A etapa 5 do [spec de melhorias](docs/superpowers/specs/2026-11-04-melhorias-road-route-helper-design.md) precisa ser concluída antes de qualquer publicação.

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Internet, porque Bootstrap, Leaflet, mapas, rotas e busca de endereços vêm de serviços externos

## Como rodar

```bash
dotnet run --project "Road Route Helper/Road Route Helper.csproj"
```

Depois abra <http://localhost:5062>.

As viagens ficam em `Road Route Helper/App_Data/trips.json`, fora do Git. Cada alteração guarda a versão anterior em `trips.json.bak`. Se o arquivo estiver corrompido, o app não inicia e não o sobrescreve: corrija-o ou restaure o `.bak`.

## Como testar

Compile uma vez e instale o Chromium usado pelos testes de navegador:

```bash
dotnet build "Road Route Helper.slnx"
```

```bash
powershell -ExecutionPolicy Bypass -File "Road Route Helper.Tests/bin/Debug/net10.0/playwright.ps1" install chromium
```

Todos os testes:

```bash
dotnet test "Road Route Helper.slnx"
```

Só os testes rápidos, sem navegador:

```bash
dotnet test "Road Route Helper.slnx" --filter "Category!=E2E"
```

Os testes de navegador simulam mapas, rotas e busca de endereços, mas carregam Bootstrap, Leaflet e SortableJS das CDNs, então precisam de internet.

## Documentação

- [Resumo técnico](Road%20Route%20Helper/PROJECT_SUMMARY.md)
- [Spec de melhorias](docs/superpowers/specs/2026-11-04-melhorias-road-route-helper-design.md)
