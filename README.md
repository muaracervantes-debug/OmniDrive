# OmniDrive

Gerenciador de armazenamento digital para desktop que reúne diferentes serviços em uma única interface.

O usuário deve poder navegar, pesquisar, organizar, enviar, baixar e transferir arquivos entre várias contas sem precisar abrir cada serviço separadamente.

## Estado atual

A solução inicial em C#/.NET está estruturada em camadas, com uma interface desktop Avalonia e contratos comuns para provedores. A integração funcional com Telegram ainda está por implementar.

## Tecnologias

- C#
- .NET 10
- Avalonia UI
- WTelegramClient (próxima etapa de integração)

## Estrutura

```text
OmniDrive/
├── src/
│   ├── OmniDrive.App/                  # Interface desktop Avalonia
│   ├── OmniDrive.Core/                 # Modelos e contratos comuns
│   ├── OmniDrive.Application/           # Orquestração compartilhada
│   └── OmniDrive.Providers.Telegram/    # Adaptador Telegram
├── tests/
│   ├── OmniDrive.Core.Tests/
│   └── OmniDrive.Application.Tests/
├── docs/
└── OmniDrive.slnx
```

## Princípios

- **Abstração por provedor:** cada serviço implementa `IStorageProvider`.
- **Capacidades explícitas:** as operações disponíveis variam por serviço e item.
- **Modelos independentes:** a interface não depende de tipos do Telegram.
- **Operações assíncronas:** rede e transferências não devem bloquear a interface.
- **Integridade dos arquivos:** validar transferências, preservar origens e evitar sobrescritas silenciosas.
- **Desempenho:** streaming, paginação, concorrência limitada e carregamento sob demanda.
- **Segurança:** credenciais e sessões fora do controle de versão, tratamento seguro de dados e dependências mantidas.
- **Testabilidade:** a lógica comum pode ser testada sem contas reais.

As regras obrigatórias estão detalhadas em [docs/engineering-principles.md](docs/engineering-principles.md).

## Executar

Instale o SDK do .NET 10. Na raiz do repositório:

```bash
dotnet restore OmniDrive.slnx
dotnet build OmniDrive.slnx
dotnet test OmniDrive.slnx
dotnet run --project src/OmniDrive.App/OmniDrive.App.csproj
```

A interface inicial é apenas a estrutura visual. A conexão com Telegram e as operações de arquivos ainda não estão implementadas.

## Telegram

O Telegram será o primeiro provedor. Como ele é uma plataforma de mensagens, não um sistema de arquivos convencional, a integração mapeará mensagens e mídias elegíveis para itens comuns e só anunciará operações realmente suportadas.

Consulte [docs/architecture.md](docs/architecture.md) para detalhes de arquitetura e sequência de implementação.
