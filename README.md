# OmniDrive

Gerenciador de armazenamento digital para desktop que reúne diferentes serviços de armazenamento em uma única interface.

O usuário deve poder navegar, pesquisar, organizar, enviar, baixar e transferir arquivos entre várias contas sem precisar abrir cada serviço separadamente.

## Objetivos

- Reunir múltiplas contas e serviços em uma interface desktop unificada.
- Oferecer operações comuns de arquivos por meio de uma experiência consistente.
- Exibir apenas as operações compatíveis com o serviço conectado e com o item selecionado.
- Permitir adicionar novos provedores sem reescrever a interface ou a lógica comum.
- Tratar diferenças e limitações de cada provedor explicitamente, sem prometer operações que ele não suporta.

## Serviços

### Telegram — primeira integração

A primeira integração será com o Telegram, usando a biblioteca [WTelegramClient](https://github.com/wiz0u/WTelegramClient). A implementação deve respeitar as capacidades e limitações da API do Telegram e distinguir arquivos/mídias de outros tipos de mensagens.

### Integrações futuras

A arquitetura deve permitir acrescentar provedores por meio de adaptadores independentes. A interface não deve depender diretamente de tipos específicos do Telegram.

## Tecnologias

- C#
- .NET
- WTelegramClient para a integração com Telegram

A versão do .NET e a tecnologia de interface desktop serão definidas durante a implementação inicial, levando em conta compatibilidade, manutenção e suporte multiplataforma quando aplicável.

## Princípios de arquitetura

1. **Abstração por provedor:** cada serviço implementa um contrato comum de integração.
2. **Capacidades explícitas:** os provedores e os itens expõem quais operações suportam; a interface usa essas informações para habilitar ou ocultar ações.
3. **Modelos comuns:** a interface trabalha com modelos independentes do provedor para itens, diretórios/locais, contas, resultados de pesquisa e operações.
4. **Separação de responsabilidades:** interface, casos de uso, contratos de domínio e integrações externas ficam separados.
5. **Operações assíncronas:** chamadas de rede e transferências não devem bloquear a interface; progresso, cancelamento quando possível e erros devem ser comunicados claramente.
6. **Segurança de credenciais:** segredos e sessões não devem ser versionados no Git nem registrados em logs. A persistência local deve usar armazenamento apropriado para credenciais.
7. **Limitações transparentes:** operações não suportadas devem ser identificadas como indisponíveis, em vez de simuladas ou falharem silenciosamente.
8. **Testabilidade:** a lógica comum deve poder ser testada sem conexão real com um serviço.

## Funcionalidades previstas

- Gerenciar conexões/contas de provedores.
- Navegar pelos itens disponibilizados por cada provedor.
- Pesquisar arquivos e mídias, respeitando os recursos de cada provedor.
- Exibir metadados relevantes, como nome, tamanho, tipo e data, quando disponíveis.
- Enviar e baixar arquivos quando suportado.
- Organizar itens quando o serviço oferecer operações correspondentes.
- Transferir arquivos entre provedores por meio de download e upload intermediários quando necessário, com tratamento de progresso, falhas e espaço temporário.
- Apresentar erros, limites e operações incompatíveis de forma compreensível.

Esta lista representa os objetivos do projeto, não uma declaração de funcionalidades já implementadas.

## Direção inicial

A primeira etapa de implementação deverá estabelecer a solução .NET, a aplicação desktop, os contratos comuns de provedores e os testes. Em seguida, será integrada a autenticação do Telegram e implementado um fluxo vertical mínimo de navegação e download antes de ampliar as operações.

## Estado atual

O repositório está no estágio inicial. A documentação descreve os objetivos e as diretrizes arquiteturais; as funcionalidades listadas ainda precisam ser implementadas e verificadas.
