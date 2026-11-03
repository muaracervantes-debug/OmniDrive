# OmniDrive

Gerenciador de armazenamento digital para desktop que reúne serviços de armazenamento em uma única interface.

O objetivo é permitir navegar, pesquisar, organizar, enviar, baixar e transferir arquivos sem precisar abrir cada serviço separadamente.

## Estado atual

O aplicativo desktop em C#/.NET 10 usa Avalonia e possui uma integração funcional com Telegram implementada atualmente na camada de interface (OmniDrive.App). Entre os recursos já desenvolvidos estão conexão e autenticação do Telegram, listagem e organização de mídias, favoritos, upload, download, divisão de arquivos, fila de transferências, verificação SHA-256, criptografia e senha de inicialização.

**Atenção à arquitetura:** os contratos independentes de provedor já existem em OmniDrive.Core e OmniDrive.Application, mas a integração Telegram ainda não foi migrada para o projeto OmniDrive.Providers.Telegram. Portanto, a aplicação ainda não utiliza esses contratos como caminho principal para executar as operações reais.

A solução está em desenvolvimento. A compilação e os testes automatizados devem ser verificados antes de considerar qualquer versão estável.

## Tecnologias

- C#
- .NET 10
- Avalonia UI
- WTelegramClient
- xUnit para testes automatizados

## Estrutura

    OmniDrive/
    ├── src/
    │   ├── OmniDrive.App/                  # Interface desktop e integração Telegram atual
    │   ├── OmniDrive.Core/                 # Modelos e contratos comuns
    │   ├── OmniDrive.Application/          # Registro e orquestração compartilhada
    │   └── OmniDrive.Providers.Telegram/   # Destino da futura migração do adaptador Telegram
    ├── tests/
    │   ├── OmniDrive.Core.Tests/
    │   └── OmniDrive.Application.Tests/
    ├── docs/
    └── OmniDrive.slnx

## Princípios

- **Abstração por provedor:** cada serviço deve implementar IStorageProvider.
- **Capacidades explícitas:** as operações disponíveis variam por serviço e item.
- **Modelos independentes:** a interface não deve depender de tipos do Telegram.
- **Operações assíncronas:** rede e transferências não devem bloquear a interface.
- **Integridade dos arquivos:** validar transferências, preservar origens e evitar sobrescritas silenciosas.
- **Desempenho:** streaming, paginação, concorrência limitada e carregamento sob demanda.
- **Segurança:** credenciais e sessões fora do controle de versão, tratamento seguro de dados e dependências mantidas.
- **Testabilidade:** a lógica comum deve poder ser testada sem contas reais.

As regras obrigatórias estão detalhadas em docs/engineering-principles.md. A arquitetura planejada está descrita em docs/architecture.md.

## Executar

Instale o SDK do .NET 10. Na raiz do repositório:

    dotnet restore OmniDrive.slnx
    dotnet build OmniDrive.slnx
    dotnet test OmniDrive.slnx
    dotnet run --project src/OmniDrive.App/OmniDrive.App.csproj

## Testes e publicação

O workflow Build Windows preview restaura a solução, executa os testes automatizados e, somente se eles passarem, publica o executável Windows x64 como artefato do GitHub Actions.

## Telegram

O Telegram é a primeira integração. Como é uma plataforma de mensagens, e não um sistema de arquivos convencional, a integração mapeia mensagens e mídias elegíveis para itens comuns e deve expor somente operações realmente suportadas pela API.

A implementação atual ainda está concentrada em OmniDrive.App; a migração para OmniDrive.Providers.Telegram é uma etapa arquitetural futura.
