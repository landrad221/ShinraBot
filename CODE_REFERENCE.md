# Referência das funções

Este documento descreve as funções implementadas no código-fonte de ShinraBot. Os tipos citados são os tipos C# usados nas assinaturas. `Task` representa uma operação assíncrona sem valor de retorno; `Task<T>` representa uma operação assíncrona que retorna um valor do tipo `T`.

## Inicialização: `Program`

### `Main()`

**Assinatura:** `static Task Main()`  
**Argumentos:** nenhum.  
**Retorno:** `Task`; permanece ativo até o processo ser encerrado.

Carrega `.env` se o arquivo existir, prepara a conexão do banco e inicializa o schema. Lê `DATABASE_PROVIDER`, `DATABASE_CONNECTION_STRING` e `BOT_DISCORD_TOKEN`; SQLite em `db/level.db` é o padrão. Cria o cliente do Discord com intents de conteúdo de mensagens e membros, registra os serviços no contêiner de DI, registra os módulos de comandos slash e conecta os eventos de mensagens, membros e exclusões aos serviços do bot. Por fim, conecta o cliente do Discord.

### `RunEventAsync(Func<Task> handler)`

**Argumentos:**

- `handler` (`Func<Task>`): função assíncrona que processa um evento do Discord.

**Retorno:** `Task`.

Executa o handler e captura exceções para escrevê-las em `Console.Error`, evitando que uma exceção do evento derrube o processo do bot.

## Persistência: `DBHandler`

### `DBHandler(string provider, string connectionString)`

**Argumentos:**

- `provider` (`string`): nome do provider, `sqlite`, `postgres` ou `postgresql`, sem distinção entre maiúsculas e minúsculas.
- `connectionString` (`string`): string de conexão do provider escolhido.

**Retorno:** uma instância de `DBHandler`.

Valida e guarda a configuração do banco. Para SQLite em arquivo, cria o diretório pai do arquivo, se necessário. Um provider desconhecido gera `ArgumentException`.

### `InitializeAsync()`

**Argumentos:** nenhum.  
**Retorno:** `Task`.

Abre uma conexão e prepara o banco. No SQLite habilita WAL. Cria as tabelas `Users`, `RoleRewards` e `BlacklistedWords`. Se encontra o schema legado de `Users` sem a coluna `GuildId`, renomeia a tabela antiga e importa os dados para o novo schema usando o guild ID `0`, preservando o XP preexistente.

### `GetUserXpAsync(ulong guildId, ulong userId)`

**Argumentos:**

- `guildId` (`ulong`): ID do servidor Discord.
- `userId` (`ulong`): ID do usuário Discord.

**Retorno:** `Task<int>`; XP do usuário no servidor, ou `0` se não houver registro.

Consulta primeiro o XP específico do servidor. Se não existir, pode retornar o saldo importado do schema legado, armazenado sob o guild ID `0`.

### `AddUserXpAsync(ulong guildId, ulong userId, int amount)`

**Argumentos:**

- `guildId` (`ulong`): ID do servidor Discord.
- `userId` (`ulong`): ID do usuário Discord.
- `amount` (`int`): quantidade de XP a somar.

**Retorno:** `Task<int>`; novo total de XP.

Faz o incremento em uma única operação SQL e retorna o novo saldo. Ao criar um registro por servidor, usa o saldo legado como ponto de partida se houver um.

**Observação do comportamento atual:** no caminho `ON CONFLICT`, o SQL soma `EXCLUDED.XP`, que também contém o saldo legado. Portanto, após criar o primeiro registro por servidor para um usuário migrado, incrementos posteriores podem somar novamente o saldo legado. Isso é uma particularidade da implementação atual, não o comportamento esperado de um incremento simples.

### `SetRoleRewardAsync(ulong guildId, int level, ulong roleId)`

**Argumentos:**

- `guildId` (`ulong`): servidor ao qual a recompensa pertence.
- `level` (`int`): nível que ativa a recompensa.
- `roleId` (`ulong`): cargo Discord a conceder.

**Retorno:** `Task`.

Insere ou atualiza o cargo configurado para aquele nível e servidor.

### `GetRoleRewardsAsync(ulong guildId, int level)`

**Argumentos:**

- `guildId` (`ulong`): servidor do usuário.
- `level` (`int`): nível alcançado.

**Retorno:** `Task<IReadOnlyList<(int Level, ulong RoleId)>>`; recompensas do servidor cujo nível configurado seja menor ou igual ao nível informado, ordenadas pelo nível.

### `AddBlacklistedWordAsync(ulong guildId, string word)`

**Argumentos:**

- `guildId` (`ulong`): servidor onde a palavra será bloqueada.
- `word` (`string`): texto que o filtro deve detectar.

**Retorno:** `Task`.

Adiciona a palavra ao filtro do servidor. Repetir a mesma inserção não cria duplicatas.

### `RemoveBlacklistedWordAsync(ulong guildId, string word)`

**Argumentos:**

- `guildId` (`ulong`): servidor do qual remover a palavra.
- `word` (`string`): texto a remover.

**Retorno:** `Task`.

Remove a palavra do filtro daquele servidor, se ela estiver cadastrada.

### `GetBlacklistedWordsAsync(ulong guildId)`

**Argumentos:**

- `guildId` (`ulong`): servidor cujas palavras serão consultadas.

**Retorno:** `Task<IReadOnlyList<string>>`; palavras bloqueadas naquele servidor.

### `OpenConnectionAsync()`

**Visibilidade:** privada.  
**Argumentos:** nenhum.  
**Retorno:** `Task<DbConnection>`.

Cria e abre uma conexão `NpgsqlConnection` ou `SqliteConnection` conforme o provider selecionado.

### `AddParameter(DbCommand command, string name, object value)`

**Visibilidade:** privada e estática.  
**Argumentos:**

- `command` (`DbCommand`): comando SQL que receberá o parâmetro.
- `name` (`string`): nome do parâmetro SQL.
- `value` (`object`): valor a associar ao parâmetro.

**Retorno:** `void`.

Cria o parâmetro por meio da conexão e o adiciona ao comando SQL.

## Eventos e regras do bot: `BotServices`

### `BotServices(DBHandler database, DiscordClient discord)`

**Argumentos:**

- `database` (`DBHandler`): serviço de persistência.
- `discord` (`DiscordClient`): cliente conectado ao Discord.

**Retorno:** uma instância de `BotServices`.

Armazena as dependências e prepara os controles em memória para cooldown de XP, contagem de mensagens e cache de palavras bloqueadas. Lê `MOD_LOG_CHANNEL_ID` na criação do serviço.

### `HandleMessageAsync(MessageCreateEventArgs e)`

**Argumentos:**

- `e` (`MessageCreateEventArgs`): evento de mensagem recebida do DSharpPlus.

**Retorno:** `Task`.

Ignora bots e mensagens que não pertencem a um servidor. Remove mensagens com mais de cinco menções, `@everyone`, `@here`, seis mensagens do mesmo usuário em uma janela de oito segundos, ou uma palavra bloqueada. Registra a remoção no canal de log quando configurado. Mensagens aceitas podem conceder de 15 a 25 XP, no máximo uma vez por usuário a cada 60 segundos. Calcula o nível por `floor(sqrt(XP / 100))`, anuncia subidas de nível e concede os cargos correspondentes.

### `LogDeletedMessageAsync(MessageDeleteEventArgs e)`

**Argumentos:**

- `e` (`MessageDeleteEventArgs`): evento de mensagem excluída.

**Retorno:** `Task`.

Ignora eventos sem servidor, sem mensagem em cache ou de mensagens enviadas pelo próprio bot. Caso contrário, envia autor, canal e conteúdo truncado para o canal de log, se configurado.

### `LogMemberJoinedAsync(GuildMemberAddEventArgs e)`

**Argumentos:**

- `e` (`GuildMemberAddEventArgs`): evento de entrada de membro.

**Retorno:** `Task`.

Registra nome e ID do membro que entrou no servidor, se o canal de log estiver configurado.

### `LogMemberLeftAsync(GuildMemberRemoveEventArgs e)`

**Argumentos:**

- `e` (`GuildMemberRemoveEventArgs`): evento de saída de membro.

**Retorno:** `Task`.

Registra nome e ID do membro que saiu do servidor, se o canal de log estiver configurado.

### `SetRoleRewardAsync(ulong guildId, int level, ulong roleId)`

**Argumentos:**

- `guildId` (`ulong`): ID do servidor.
- `level` (`int`): nível da recompensa.
- `roleId` (`ulong`): ID do cargo.

**Retorno:** `Task`.

Encaminha a configuração da recompensa de nível para `DBHandler`.

### `AddBlockedWordAsync(ulong guildId, string word)`

**Argumentos:**

- `guildId` (`ulong`): ID do servidor.
- `word` (`string`): palavra ou trecho que será filtrado.

**Retorno:** `Task`.

Salva a palavra e invalida o cache local daquele servidor.

### `RemoveBlockedWordAsync(ulong guildId, string word)`

**Argumentos:**

- `guildId` (`ulong`): ID do servidor.
- `word` (`string`): palavra ou trecho a remover.

**Retorno:** `Task`.

Remove a palavra e invalida o cache local daquele servidor.

### `TrackMessage(ulong guildId, ulong userId)`

**Visibilidade:** privada.  
**Argumentos:** `guildId` (`ulong`) e `userId` (`ulong`), identificadores do servidor e usuário.

**Retorno:** `bool`; `true` quando há seis ou mais mensagens do usuário nos últimos oito segundos.

Mantém e limpa a fila de horários recentes em memória. O acesso à fila é sincronizado por lock.

### `GetBlockedWordsAsync(ulong guildId)`

**Visibilidade:** privada.  
**Argumentos:** `guildId` (`ulong`), servidor a consultar.

**Retorno:** `Task<string[]>`.

Retorna as palavras bloqueadas. Mantém o resultado em cache por 30 segundos por servidor.

### `LogAsync(ulong guildId, string message)`

**Visibilidade:** privada.  
**Argumentos:** `guildId` (`ulong`), servidor do evento; `message` (`string`), texto para registrar.

**Retorno:** `Task`.

Envia o texto ao canal indicado por `MOD_LOG_CHANNEL_ID` somente se esse canal pertencer ao servidor informado. Não envia nada se a variável não estiver configurada.

### `Truncate(string value, int length)`

**Visibilidade:** privada e estática.  
**Argumentos:** `value` (`string`), texto de entrada; `length` (`int`), tamanho máximo antes da reticência.

**Retorno:** `string`.

Devolve o texto original se ele couber no limite; caso contrário, devolve o prefixo limitado seguido de `...`.

## Comandos slash

### `UtilityCommands(DBHandler database)`

**Argumentos:** `database` (`DBHandler`), serviço usado para consultar XP.  
**Retorno:** uma instância do módulo de comandos utilitários.

### `PingAsync(InteractionContext context)`

**Argumentos:** `context` (`InteractionContext`), contexto da interação slash.

**Retorno:** `Task`.

Responde com `Pong!` e a latência do cliente, em milissegundos.

### `RankAsync(InteractionContext context)`

**Argumentos:** `context` (`InteractionContext`), contexto da interação slash.

**Retorno:** `Task`.

Exige execução em servidor, consulta o XP do usuário que executou o comando, calcula o nível e responde com um anexo `rank.png` renderizado pelo bot.

### `UtilityCommands.RespondAsync(InteractionContext context, string message)`

**Visibilidade:** privada e estática.  
**Argumentos:** `context` (`InteractionContext`), interação a responder; `message` (`string`), conteúdo da resposta.

**Retorno:** `Task`.

Cria uma resposta de interação do tipo `ChannelMessageWithSource` com o texto fornecido.

### `ModerationCommands(BotServices botServices)`

**Argumentos:** `botServices` (`BotServices`), serviço usado para configurar recompensas e filtros.  
**Retorno:** uma instância do módulo de moderação.

### `BanAsync(InteractionContext context, DiscordUser user, string reason = "No reason provided")`

**Argumentos:**

- `context` (`InteractionContext`): contexto do comando.
- `user` (`DiscordUser`): usuário alvo.
- `reason` (`string`, opcional): motivo registrado no banimento; padrão `No reason provided`.

**Retorno:** `Task`.

Bane o usuário do servidor e responde com o resultado. O comando exige a permissão Discord `BanMembers`.

### `KickAsync(InteractionContext context, DiscordUser user, string reason = "No reason provided")`

**Argumentos:** `context` (`InteractionContext`), interação; `user` (`DiscordUser`), alvo; `reason` (`string`, opcional), motivo.

**Retorno:** `Task`.

Busca o membro no servidor, expulsa-o e responde com o motivo. Exige `KickMembers`.

### `TimeoutAsync(InteractionContext context, DiscordUser user, long minutes, string reason = "No reason provided")`

**Argumentos:** `context` (`InteractionContext`), interação; `user` (`DiscordUser`), alvo; `minutes` (`long`), duração em minutos; `reason` (`string`, opcional), motivo.

**Retorno:** `Task`.

Aplica o timeout configurado por `minutes`. Exige `ModerateMembers`.

### `MuteAsync(InteractionContext context, DiscordUser user, long minutes, string reason = "No reason provided")`

**Argumentos:** iguais aos de `TimeoutAsync`.

**Retorno:** `Task`.

Alias slash `/mute` para a mesma operação de timeout. Exige `ModerateMembers`.

### `ApplyTimeoutAsync(InteractionContext context, DiscordUser user, long minutes, string reason)`

**Visibilidade:** privada e estática.  
**Argumentos:** `context` (`InteractionContext`), interação; `user` (`DiscordUser`), alvo; `minutes` (`long`), duração; `reason` (`string`), motivo.

**Retorno:** `Task`.

Rejeita durações fora do intervalo de 1 a 40320 minutos (28 dias). Caso válido, aplica o timeout e responde à interação.

### `RewardRoleAsync(InteractionContext context, long level, DiscordRole role)`

**Argumentos:** `context` (`InteractionContext`), interação; `level` (`long`), nível requerido; `role` (`DiscordRole`), cargo a conceder.

**Retorno:** `Task`.

Registra a recompensa para o servidor. O nível deve ser positivo e caber em `int`. Exige `ManageGuild`.

### `BlockWordAsync(InteractionContext context, string word)`

**Argumentos:** `context` (`InteractionContext`), interação; `word` (`string`), palavra ou trecho a bloquear.

**Retorno:** `Task`.

Remove espaços no início e no fim, exige entre 1 e 100 caracteres e adiciona a palavra ao filtro do servidor. Exige `ManageGuild`.

### `UnblockWordAsync(InteractionContext context, string word)`

**Argumentos:** `context` (`InteractionContext`), interação; `word` (`string`), palavra ou trecho a liberar.

**Retorno:** `Task`.

Remove espaços nas extremidades, valida o limite de 1 a 100 caracteres e retira a palavra do filtro. Exige `ManageGuild`.

### `IsGuildCommandAsync(InteractionContext context)`

**Visibilidade:** privada e estática.  
**Argumentos:** `context` (`InteractionContext`), contexto da interação.

**Retorno:** `Task<bool>`; `true` se a interação veio de um servidor, caso contrário envia uma resposta explicativa e retorna `false`.

### `ModerationCommands.RespondAsync(InteractionContext context, string message)`

**Visibilidade:** privada e estática.  
**Argumentos:** `context` (`InteractionContext`), interação a responder; `message` (`string`), conteúdo da resposta.

**Retorno:** `Task`.

Envia uma resposta textual à interação slash.

## Renderização: `RankCardRenderer`

### `Render(string username, int level, int xp)`

**Argumentos:**

- `username` (`string`): nome exibido no cartão e usado para derivar a cor de destaque.
- `level` (`int`): nível que será exibido.
- `xp` (`int`): XP total que será exibido e usado para calcular a barra de progresso.

**Retorno:** `byte[]`; conteúdo PNG do cartão, com dimensões de 900 × 280 pixels.

Renderiza fundo, nome, nível, XP e progresso para o próximo nível usando SkiaSharp. A progressão corresponde à fórmula de nível `floor(sqrt(XP / 100))`.
