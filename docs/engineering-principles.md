# Princípios de engenharia

Estas regras são obrigatórias para toda implementação do OmniDrive e devem orientar decisões de arquitetura, revisão e testes.

## 1. Integridade dos arquivos

- Nunca sobrescrever ou excluir conteúdo silenciosamente. Operações destrutivas devem exigir uma ação explícita e apresentar claramente o alvo.
- Em transferências, validar tamanho quando conhecido e calcular hashes criptográficos (preferencialmente SHA-256) quando houver acesso aos bytes de origem e destino.
- Considerar uma transferência concluída somente depois de finalizar a gravação, validar o resultado e confirmar que o destino foi fechado/liberado corretamente.
- Para gravações locais, usar arquivo temporário no mesmo volume e renomeação atômica quando suportada; não substituir o destino existente sem política explícita.
- Em falhas ou cancelamento, preservar a origem, limpar temporários com segurança e informar se o destino ficou parcial.
- Não manter arquivos inteiros em memória. Preferir streams e buffers limitados, com limites configuráveis.
- Testar arquivos vazios, arquivos grandes, interrupções, cancelamento, falta de espaço, erros de rede, nomes conflitantes e corrupção simulada.
- Não alegar verificação ponta a ponta quando o provedor não oferece dados suficientes para confirmar a integridade.

## 2. Desempenho e consumo de recursos

- Usar I/O assíncrono e streaming; nunca bloquear a thread da interface com operações de rede ou disco.
- Evitar carregar diretórios/históricos inteiros quando houver paginação, busca incremental ou carregamento sob demanda.
- Definir limites explícitos para concorrência, buffers, cache e espaço temporário.
- Cancelar trabalho obsoleto quando o usuário trocar de conta, localização ou pesquisa.
- Evitar polling frequente, consultas duplicadas e re-renderizações desnecessárias; reutilizar resultados em cache apenas quando a invalidação for segura.
- Medir antes de otimizar e acompanhar memória, CPU, tempo de resposta e vazão em cenários representativos.
- Preferir dependências pequenas e mantidas; justificar novas dependências pelo benefício concreto.

## 3. Segurança

- Nunca versionar credenciais, API hashes, códigos de autenticação, tokens, cookies ou sessões reais; não incluí-los em logs, exceções exibidas ou telemetria.
- Guardar segredos usando armazenamento seguro do sistema operacional sempre que disponível. Configuração por variável de ambiente deve ser usada com cautela e nunca confundida com armazenamento seguro persistente.
- Aplicar o princípio do menor privilégio e separar os dados de cada conta/provedor.
- Validar nomes de arquivos e caminhos, evitar path traversal e não confiar em metadados fornecidos por serviços remotos.
- Usar TLS e bibliotecas oficiais/mantidas; não desabilitar validação de certificados.
- Tratar arquivos baixados como conteúdo não confiável. Não executar arquivos nem abrir automaticamente conteúdo baixado.
- Não registrar conteúdo de arquivos, dados privados de mensagens ou informações sensíveis sem necessidade explícita.
- Tornar operações destrutivas e mudanças de conta explícitas, com confirmação quando apropriado.
- Manter dependências atualizadas e revisar vulnerabilidades antes de releases.

## 4. Aplicação prática e revisão

Toda funcionalidade que manipule arquivos deve incluir tratamento de erros, cancelamento quando possível e testes para falhas. Alterações que afetem segurança, integridade ou desempenho devem documentar suas premissas e limitações.

Uma operação não deve ser descrita como segura, íntegra ou validada sem testes ou evidências correspondentes.
