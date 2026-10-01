---
title: Tilettes
---

# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · **Português** · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Um painel de inicialização rápida para Windows: uma grade de mosaicos com atalhos, pastas e abas, pesquisa aproximada (fuzzy) integrada e um mini explorador de arquivos com console embutido. Um único EXE portátil, sem instalador, .NET Framework 4.8 (WinForms).

Versão atual: **v0.6.0-beta** — download: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Histórico de alterações](#changelog). Status: **beta**.

## Recursos

- **Painel** — mosaicos de 1×1…6×6, abas ilimitadas (arrastáveis, em várias linhas), pastas abertas dentro da própria aba ou em janelas pop-up, arrastar e soltar do Explorador de Arquivos, grade personalizada (colunas/linhas/transparência), escala de ícones.
- **Pesquisa** — pesquisa nomes, nomes de arquivos, metadados de programas (FileDescription / ProductName / CompanyName), caminhos completos e descrições do usuário; correspondência aproximada (fuzzy) com precisão ajustável e correção de layout de teclado errado (`руддщ` → `hello`); os resultados são classificados pela qualidade da correspondência e os caracteres correspondentes são destacados.
- **Mini Explorer** — navegação por trilha (breadcrumbs), favoritos de pastas/comandos/grupos, pesquisa de arquivos (pasta atual ou todos os discos fixos) com índice em segundo plano e um console `cmd.exe` embutido com histórico de comandos, comandos salvos e zoom da fonte com Ctrl+roda do mouse.
- **Regras de tipos de arquivo** — ícones e associações de "abrir com" por extensão/máscara, importação/exportação.
- **Integração com o desktop** — ícone na bandeja, inicialização automática com o Windows, tecla de atalho global, menus de contexto nativos do Explorador de Arquivos, janela sem bordas com redimensionamento pelas bordas.
- **Primeira execução e atualizações** — uma janela de boas-vindas exibida uma única vez (aviso de beta, escolha de idioma, permissão de verificação de atualizações, mosaicos de exemplo) e uma verificação de atualizações no GitHub Releases com uma placa no canto quando existe uma versão mais nova.

## Primeira execução e atualizações

- **Janela de boas-vindas** (somente na primeira execução de todas): uma mensagem de agradecimento, um aviso de beta com link para [Issues](https://github.com/AlexNoVibe/Tilettes/issues), um mini-diagrama desenhado de "arraste um atalho → vira um mosaico", escolha de idioma (bandeiras RU/EN), permissão de verificação de atualizações, endereços de doação (clique para copiar) — e dois botões de saída: simplesmente **Fechar**, ou **Fechar e criar mosaicos de exemplo** (Bloco de Notas, Calculadora, Explorador de Arquivos e Paint como mosaicos prontos). Pode ser exibida novamente a qualquer momento pelo item "Mostrar a janela de boas-vindas novamente" nas configurações.
- **Verificação de atualizações** — o aplicativo consulta a API pública do GitHub Releases uma vez a cada N dias (padrão: 3; a primeira verificação também acontece N dias após a primeira execução, não imediatamente). Nada é enviado a lugar algum e, com a verificação desativada nas configurações, nenhuma requisição de rede é feita. Quando existe uma tag mais nova, uma placa verde **⟳ Atualizar** aparece ao lado do botão de configurações e abre a página de [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest). "Verificar agora" nas configurações faz uma verificação manual independentemente do intervalo (o resultado é informado em uma caixa de mensagem). A instalação automática ainda é um stub (TODO).
- **Gancho de teste** — inicie o aplicativo com `WINPANEL_MOCK_UPDATE=0.6` para exibir a placa de Atualizar como se existisse um release mais novo (sem envolver a rede).

## Referência de configurações

Todas as configurações ficam em uma única janela (botão ⚙ / menu da bandeja) e são armazenadas em `settings.ini`.

### Inicialização e janela

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Tamanho na inicialização (W × H) | 200–4000 | 900 × 800 | Tamanho do painel a cada inicialização. O redimensionamento durante uma sessão não é persistido — apenas a posição é. |
| Posição da janela (X, Y) | −4000…4000 | 100, 100 | Posição na tela ao iniciar. Atualizada automaticamente quando a janela é movida. |
| Tecla de atalho para mostrar a janela | predefinições + personalizada | Ctrl+Q | Tecla de atalho global que mostra/ativa o painel. Escolha uma predefinição (None, Ctrl+Q, Ctrl+Shift+Q, Alt+Q, Ctrl+J, …) ou digite qualquer combinação `Mod+Tecla` (Ctrl/Alt/Shift/Win + uma letra ou dígito) diretamente no campo editável; uma entrada não reconhecida é rejeitada com uma explicação. |
| Idioma | ru / en | ru | Idioma da interface, aplicado imediatamente. |

### Grade e mosaicos

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Transparência da grade | 0–255 | 50 | Alfa das linhas da grade. 0 = invisível. Desenhada apenas quando a grade (botão ▦) está ativada. |
| Colunas da grade | 1–100 | 16 | Células horizontais. As posições dos mosaicos se ajustam a esta grade. |
| Linhas da grade | 1–100 | 16 | Células verticais. |
| Tamanho padrão do item | 1–6 | 2 | Tamanho dos mosaicos recém-adicionados (1×1 … 6×6 células). |
| Escala dos ícones (%) | 25–400 | 100 | Tamanho do ícone dentro de um mosaico, em porcentagem do padrão. |
| Permitir adicionar ícones | ativado/desativado | ativado | Modo de edição: arrastar mosaicos, criar pastas, receber arquivos. Quando desativado, os mosaicos simplesmente são executados ao clicar. |
| Skin e tema | Nenhum (escuro) / Claro / skins | Nenhum (escuro) | Tema clássico escuro ou claro, ou uma skin decorativa (cores próprias + borda da janela). |

### Pastas

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Abrir pastas em | Mesma janela / Janela pop-up | Mesma janela | Clicar em uma pasta navega para dentro da aba ou abre uma janela pop-up acima de tudo. |
| s de inatividade | 0–600 | 15 | Apenas no modo "Mesma janela": volta automaticamente para cima após N segundos sem atividade de mouse/teclado. 0 = desativado. |

### Fontes

Uma linha para cada grupo: **Mosaicos**, **Abas** e **Interface**:

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| tamanho | 6–24 | 9 | Tamanho da fonte do grupo. |
| amostra de cor | qualquer cor | vazia | Cor de texto personalizada; vazia = padrão do tema. Aplica-se aos rótulos dos mosaicos, aos títulos das abas ou a todo o texto da interface. |
| família | qualquer fonte instalada | Segoe UI | Família de fontes do grupo. |

### Pesquisa

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Precisão da pesquisa aproximada (0–3) | 0–3 | 2 | 0 = apenas correspondências de substring; 1–3 = correspondência aproximada cada vez mais tolerante a erros de digitação. Dígitos contam em dobro, então códigos numéricos correspondem de forma estrita. |
| Pesquisar nos metadados | ativado/desativado | ativado | Nome do arquivo, destino do atalho, informações de versão (descrição, produto, empresa). |
| Pesquisar nos caminhos completos | ativado/desativado | ativado | O texto do caminho completo, incluindo as pastas pai. |
| Pesquisar nas descrições | ativado/desativado | ativado | Descrições do usuário (clique direito → Descrição…). |
| Fonte da pesquisa: campo | 7–30 | 9 | Tamanho da fonte do campo de pesquisa. |
| Fonte da pesquisa: resultados | 7–30 | 9 | Tamanho da fonte das linhas de resultados (a altura da linha acompanha a fonte). |

### Atualizações

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Verificar atualizações automaticamente | ativado/desativado | ativado | Consulta o GitHub Releases por uma versão mais nova uma vez a cada N dias. Nunca executa quando desmarcado — nenhuma requisição de rede. |
| Verificar a cada N dias | 1–365 | 3 | Com que frequência verificar. A primeira verificação acontece N dias após a primeira execução. |
| Verificar agora | botão | — | Consulta o GitHub Releases imediatamente (a verificação manual funciona mesmo com a automática desativada). |
| Instalar atualizações automaticamente | ativado/desativado | desativado | **Stub (TODO)** — ainda não implementado. |
| ♥ Doar | botão | — | Lista pop-up de carteiras (um clique copia o endereço) mais a seção de doação no GitHub. |
| Mostrar a janela de boas-vindas novamente | botão | — | Reproduz a janela de boas-vindas da primeira execução. |

### Mini Explorer (chaves INI)

| Chave | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Ctrl+clique em uma pasta abre o Mini Explorer | ativado/desativado | ativado | O atalho Ctrl+clique em mosaicos de pasta. |
| `MiniExplorerW/H/X/Y` | W≥760, H≥520 | automático | Geometria da janela, memorizada ao fechar. |
| `MiniExplorerBookmarks` | ativado/desativado | ativado | Visibilidade do painel lateral de favoritos. |
| `MiniExplorerTopBar` | ativado/desativado | ativado | Visibilidade da barra horizontal de favoritos. |
| `MiniExplorerConsole` | 15–85 | 40 | Altura do console como porcentagem da janela. |
| `ConsoleFontSizeX10` | 60–280 | 85 | Tamanho da fonte do console ×10 (85 = 8,5 pt), alterado com Ctrl+roda do mouse. |

### Inicialização automática e bandeja

| Configuração | Intervalo | Padrão | Descrição |
|---|---|---|---|
| Inicialização automática com o Windows | ativado/desativado | desativado | Grava em `HKCU\...\Run` ("Tilettes"). |
| Após a inicialização automática — ir para a bandeja | ativado/desativado | desativado | Adiciona `--minimized`: o painel inicia oculto na bandeja. |
| Minimizar em vez de fechar | ativado/desativado | ativado | ✕ / Alt+F4 oculta para a bandeja (ou minimiza) em vez de encerrar. Sair está no menu da bandeja. |
| Manter sempre o ícone na bandeja | ativado/desativado | ativado | Ícone da bandeja visível o tempo todo. |
| Lembrar a aba ativa | ativado/desativado | ativado | Restaura a última aba ativa ao iniciar. |

### Backup e sincronização do Menu Iniciar

- **Fazer backup agora** — backup zip completo em `autoBackup\` (configurações, mosaicos, ícones, favoritos, histórico de pesquisa, o exe); agendado por "Fazer backup a cada N dias" (0 = desativado), criado ~3 minutos após a inicialização quando é a hora.
- **Salvar backup (zip)** — o mesmo arquivo compactado em um destino escolhido pelo usuário.
- **Restaurar a partir de arquivo…** — espera um zip criado pelo próprio Tilettes; os arquivos são descompactados na pasta de trabalho, `Tilettes.exe` nunca é substituído.
- **Sincronizar o Menu Iniciar agora** / a cada N horas (0 = desativado) — reconstrói a aba espelhada do Menu Iniciar.

## Teclas de atalho e comandos

### Painel principal

| Teclas / ação | Resultado |
|---|---|
| Tecla de atalho (padrão Ctrl+Q) | Mostrar / ativar o painel. |
| Digite qualquer texto, ou Ctrl+F | Abre a pesquisa do painel. |
| ↓ | Pular para a lista de resultados. |
| Enter | Abre o resultado selecionado (pasta → navega, arquivo → executa). |
| Esc | Fecha a pesquisa. |
| Clique em um mosaico | Executa o item; pasta navega (ou pop-up, conforme as configurações). |
| Ctrl+clique em um mosaico de pasta | Abre o Mini Explorer (se ativado). |
| Arrastar um mosaico (modo de edição) | Move-o; soltar sobre uma pasta o move para dentro. |
| Soltar arquivos no painel (modo de edição) | Adiciona como mosaicos (soltar sobre uma pasta adiciona para dentro). |
| Clique direito em um mosaico | Menu nativo do Explorador de Arquivos mais: Descrição…, Tamanho 1×1–6×6, Renomear, Alterar ícone, Remover, Mover para fora da pasta, Abrir no Mini Explorer (pastas). |
| Clique direito em uma aba | Excluir (a última aba é protegida), Renomear, Alternar layout livre/grade. |
| Arrastar uma aba | Reordenar dentro de uma linha ou mover para outra linha. |
| Clique direito em área vazia do painel | Criar pasta, Configurações. |
| Botões ▦ / ✅ / ⚙ | Visibilidade da grade, modo de edição, configurações. |

### Mini Explorer

| Teclas / ação | Resultado |
|---|---|
| Ctrl+L / F4 / Editar | Editar o caminho. |
| F5 | Atualizar a pasta. |
| Backspace | Subir um nível. |
| Alt+← / Alt+→ | Voltar / avançar. |
| Enter / duplo clique | Abrir (pasta navega, arquivo executa). |
| Esc | Sair da pesquisa → cancelar a edição do caminho → fechar a janela. |
| Digitar na lista de arquivos | Inicia uma pesquisa no campo de pesquisa. |
| ↓ / ↑ (na pesquisa) | Move pelos resultados. |
| Ctrl+roda do mouse | Tamanho da fonte do console (persistido). |
| Arrastar o divisor | Altura do console (persistida). |
| Botões ≡ / ☰ | Alternar painel lateral de favoritos / barra superior de favoritos. |
| Clique direito em um arquivo | Abrir, Mostrar no Explorador de Arquivos, Copiar caminho. |
| Clique direito em uma pasta | Abrir, Adicionar aos favoritos, Abrir no Explorador de Arquivos. |
| Clique direito em área vazia | Atualizar, Copiar caminho da pasta, Abrir no Explorador de Arquivos, Adicionar a pasta atual aos favoritos, Abrir uma janela de console aqui. |
| Clique direito em um favorito | Editar comando… (somente comandos), Renomear…, Mover para cima / Mover para baixo, Remover. |

### Console

Qualquer comando `cmd.exe` de uma única linha pode ser digitado e executado (Enter ou **Executar**). O diretório de trabalho é ressincronizado com a pasta atual antes de cada comando. **+ Salvar** armazena o comando digitado como um favorito (opcionalmente dentro de um grupo); comandos salvos são executados ao clicar. Botões: **Limpar** (apagar a saída), **Reiniciar** (novo cmd.exe), **Nova janela** (uma janela de console real na pasta atual). O histórico de comandos está disponível com ↑ / ↓ durante a sessão.

## Limitações

- **Somente Windows + .NET Framework 4.8** (GDI/WinForms). Sem suporte a DPI por monitor — a interface pode ficar borrada em telas com escala acentuada.
- **O escopo de pesquisa "Todos"** indexa **somente discos locais fixos** (sem unidades USB/de rede), com limite de **200 000 itens por disco**; a indexação roda em segundo plano, então os resultados crescem enquanto ela trabalha ("indexando: N" na linha de status).
- **A pesquisa do painel** mostra as **200** melhores correspondências; **a pesquisa do Mini Explorer** retorna até **400**; uma lista de arquivos mostra no máximo **800** entradas por diretório.
- **O console é apenas `cmd.exe`**: comandos de uma linha; programas interativos/TUI (editores, paginadores com entrada de teclado) não funcionam corretamente; o buffer de saída é limpo automaticamente após ~150 000 caracteres; a codificação segue a página de código OEM do sistema (por exemplo, CP866).
- **A tecla de atalho global** é uma letra/dígito mais modificadores; o registro falha com um balão de notificação se outro programa já estiver usando a combinação.
- **O tamanho do painel volta ao tamanho de inicialização a cada execução** — apenas a posição é lembrada (por design).
- **Arrastar e soltar e mover mosaicos exigem o modo de edição** ("Permitir adicionar ícones" / botão ✅).
- Mosaicos de pasta mostram no máximo **9** ícones filhos na pré-visualização; o pop-up da pasta mostra no máximo **4** colunas por linha.
- Itens `.lnk`/`.ico` adicionados ao painel são **copiados para `ico\`** para sobreviverem à movimentação dos originais.
- **Restaurar a partir de arquivo** aceita apenas zips criados pelo Tilettes ("Fazer backup agora" / "Salvar backup (zip)").
- Os cantos arredondados da janela são removidos temporariamente durante o redimensionamento (técnica para evitar cintilação) e restaurados ao soltar.
- A correção de layout cobre o par EN↔RU QWERTY; outros layouts passam sem alteração.
- **Instância única**: iniciar uma segunda cópia apenas mostra a janela existente.
- A saída automática de pastas funciona apenas no modo "Mesma janela" e apenas enquanto estiver dentro de uma pasta.

## Compilar

Requer qualquer Windows com .NET Framework 4.x (o compilador acompanha o sistema operacional):

```
build.bat
```

ou diretamente:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /out:Tilettes.exe src\*.cs
```

Os releases são criados automaticamente pelo GitHub Actions a cada tag `v*`: o workflow compila as variantes do exe com a mesma chamada do csc e anexa arquivos exe simples ao release (AnyCPU universal + x86 + x64 — sem zip); os arquivos de código-fonte automáticos do GitHub também estão no release. Você também pode compilar o exe você mesmo com `build.bat`.

## Arquivos de dados (criados junto ao EXE)

| Arquivo | Finalidade |
|---|---|
| `settings.ini` | Todas as configurações |
| `records.xml` | Abas, pastas, atalhos, descrições |
| `bookmarks.xml` | Favoritos do Mini Explorer |
| `filetypes.xml` | Regras de tipos de arquivo |
| `ico\` | Cópias de itens .lnk/.ico e ícones personalizados |

## Estrutura do projeto (`src/`)

| Arquivo | Finalidade |
|---|---|
| `Program.cs` | Janela principal: abas, mosaicos, pesquisa do painel, pop-ups de pastas, instância única |
| `MiniExplorerForm.cs` | Mini Explorer: navegação, favoritos, console embutido |
| `SearchCore.cs` | Indexação de discos, pré-filtro por máscara de bits, pontuação da pesquisa aproximada |
| `PanelSearch.cs` | Metadados pesquisáveis dos itens salvos |
| `Settings.cs` / `SettingsForm.cs` | Modelo e janela de configurações |
| `FileTypes.cs` / `FileTypesForm.cs` | Regras de tipos de arquivo e seus editores |
| `bookmarks.cs` | Armazenamento de favoritos |
| `loc.cs` + `lang_*.cs` | Localização: EN como fonte, RU em linha, tabelas ES/PT/DE/FR/IT/PL/ZH/JA |
| `UpdateChecker.cs` / `WelcomeForm.cs` | Verificação de atualizações (GitHub Releases) e janela de boas-vindas da primeira execução |
| `IconExtractor.cs`, `NativeContextMenu.cs`, `IniFile.cs`, `Records.cs`, `apputil.cs` | Ícones, menus nativos, E/S de INI, E/S de registros, inicialização automática/instância única/carteiras |

## Licença

[MIT](LICENSE) — livre para usar, modificar e distribuir.

<a name="donate"></a>
## Doar

Se o Tilettes é útil para você, você pode apoiar o desenvolvimento com cripto. As redes compatíveis com EVM compartilham um mesmo endereço — envie pela rede que for mais conveniente:

<a name="donate-evm"></a>
### EVM — Ethereum · Polygon · Base · Monad · HyperEVM

```
0xf84897FA0b74083c16865315A5b148f4d92e6C2a
```

<a name="donate-btc"></a>
### Bitcoin (BTC)

```
bc1qu9cf5uqc5wxqwde8mk378xwdlnjatvmhxhvat5
```

<a name="donate-sol"></a>
### Solana (SOL)

```
7ffCFnJBNVaF268FsZGKBPEWe3UNrWbasgt3aidiCw68
```

<a name="donate-sui"></a>
### Sui (SUI)

```
0x3ca194b355bb00a1f5f646786407ebbcdaee361c6f56fb92f8df9abd73b0c3b1
```

Outras formas de ajudar: relate bugs e ideias em [Issues](https://github.com/AlexNoVibe/Tilettes/issues), dê uma estrela ao repositório, espalhe a palavra.

<a name="changelog"></a>
## Histórico de alterações

### v0.6.0-beta (2026-10-01)

- Desempenho: inicialização mais rápida (renderização das abas sob demanda — apenas a aba ativa é construída), cache persistente de ícones (ícones do shell são extraídos uma única vez durante a vida do mosaico), metadados de pesquisa coletados uma única vez quando um item é adicionado; o campo de pesquisa do Mini Explorer está desativado (o código foi mantido para reativação futura).
- Compartilhamentos de rede nunca bloqueiam a thread da interface: ícones e destinos `.lnk` em caminhos de rede são resolvidos em segundo plano.
- Uma skin ativa agora define a paleta clara/escura em todas as janelas (escolher Mint não deixa mais janelas escuras); mint é o tema padrão e todas as fontes passam a ser 14 por padrão.
- Primeira execução: em monitores com área útil abaixo de 900px, a janela e a grade padrão diminuem proporcionalmente para se ajustar à tela.
- Reforços de segurança: strong name, VERSIONINFO, manifesto explícito, captura da tecla Win é opcional (opt-in) — 0 detecções no VirusTotal.
- Os artefatos do release são arquivos exe simples por CPU (AnyCPU/x86/x64) em vez de um arquivo zip; as notas do release vêm do CHANGELOG.md.

### v0.5 (2026-09-30)

- Janela de boas-vindas na primeira execução (uma vez por pasta de dados): agradecimento, aviso de beta + link para issues, mini-diagrama desenhado, escolha de idioma (bandeiras RU/EN), permissão de verificação de atualizações, endereços de doação (clique para copiar); saída por "Fechar" ou "Fechar e criar mosaicos de exemplo" (Bloco de Notas / Calculadora / Explorador de Arquivos / Paint como mosaicos prontos). Pode ser exibida novamente nas configurações.
- Verificação de atualizações: o aplicativo consulta a API pública do GitHub Releases pela tag mais recente a cada N dias (padrão 3; a primeira verificação também acontece N dias após a instalação) — estritamente somente quando o usuário permitiu; caso contrário, zero requisições de rede. Quando existe uma versão mais nova, uma placa verde "Atualizar" aparece ao lado do botão de configurações e abre a página de releases. Botão manual "Verificar agora" nas configurações (informa o resultado em uma caixa de mensagem). A instalação automática é um stub (TODO). Gancho de simulação para testar a placa: `WINPANEL_MOCK_UPDATE=0.6`.
- Configurações: nova seção "Atualizações" (alternador de verificação, intervalo em dias, botão verificar agora, stub de instalação automática, linha de doação com menu pop-up de carteiras — um clique copia o endereço — e botão para exibir novamente a janela de boas-vindas). Campo editável de tecla de atalho: qualquer combinação Ctrl/Alt/Shift/Win + letra/dígito pode ser digitada (com validação); o padrão passou a ser Ctrl+Q.
- Interface do programa localizada em **10 idiomas**: inglês (fonte), russo, espanhol, português, alemão, francês, italiano, polonês, chinês (simplificado) e japonês. O idioma é escolhido nas configurações ou pelas bandeiras desenhadas na janela de boas-vindas; novos idiomas são um arquivo de tabela + uma linha (veja loc.cs).
- Lista real de carteiras de doação agrupada por rede: as redes EVM (ETH · Polygon · Base · Monad · HyperEVM) compartilham um endereço; além de Bitcoin, Solana e Sui.
- A versão agora é uma única constante (`AppInfo.AppVersion`); o tooltip da bandeja e a janela de boas-vindas a exibem.
- Infraestrutura no GitHub: licença MIT, FUNDING.yml (links de patrocínio para as âncoras das carteiras), README dividido em arquivos por idioma (`README.md` em inglês + 9 traduções) para fácil extensão, workflow do GitHub Actions (release com um zip portátil nas tags `v*`), página de destino em docs/ para o GitHub Pages. Todas as mensagens de commit do histórico estão em inglês.

### v0.4 (2026-09-29)

- Rebranding: Tilettes / «Плиточки», novo ícone (recurso do exe + ícone de bandeja desenhado em código).
- Recurso de backup concluído: "Salvar backup (zip)" empacota configurações, mosaicos, ícones, favoritos, histórico de pesquisa e o exe; "Restaurar a partir de arquivo" descompacta zips criados pelo aplicativo na pasta de trabalho sem substituir o Tilettes.exe; backups completos agendados em autoBackup\.
- Reformulação da janela de configurações: redimensionável verticalmente por uma alça inferior, tooltips em cada item, campos X/Y rotulados para tamanho de inicialização e posição da janela, colunas/linhas da grade em uma única linha, caixa de combinação de skins no lugar da caixa de seleção duplicada de tema claro.
- Correções na aba espelhada do Menu Iniciar (o conteúdo das pastas não fica mais aglomerado; layout automático simplificado).
- Correções de layout para fontes 14–20 (abas, altura do status da pesquisa, diálogos, barras do Mini Explorer, degrau do canto da janela).

### v0.3 (2026-09-28)

- Sincronização do Menu Iniciar (aba espelhada, agendada), redução em degraus dos alternadores de pesquisa, faixa de configurações rápidas da pesquisa, modo de edição com seleção múltipla, menus "Mover para aba", captura da tecla Win, navegação em pop-ups de pastas, destaque do caminho nos resultados da pesquisa.

### v0.1 – v0.2 (2026-09-27)

- Primeiras compilações do painel iniciador: mosaicos, abas, pastas, pesquisa do painel, Mini Explorer com console, regras de tipos de arquivo, bandeja/inicialização automática/tecla de atalho, personalização da grade.
