# Tilettes

[English](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) · [Русский](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) · [Español](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.es.md) · **Português** · [Deutsch](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.de.md) · [Français](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.fr.md) · [Italiano](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.it.md) · [Polski](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.pl.md) · [中文 (简体)](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.zh.md) · [日本語](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ja.md)

Um painel de inicialização rápida para Windows: uma grade de mosaicos com atalhos, pastas e abas, pesquisa aproximada integrada e um mini explorador de arquivos com console embutido. Um único EXE portátil, sem instalador, .NET Framework 4.8 (WinForms).

Versão atual: **v0.5** — download: [Releases](https://github.com/AlexNoVibe/Tilettes/releases/latest) · [Changelog (English)](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md#changelog). Status: **beta**.

## Recursos

- Mosaicos de 1×1…6×6, abas ilimitadas arrastáveis, pastas dentro da aba ou em janelas pop-up, arrastar e soltar do Explorador de Arquivos, grade personalizada, escala de ícones.
- Pesquisa aproximada por nomes, metadados, caminhos e descrições, com correção de teclado em layout diferente.
- Mini Explorer com trilha de navegação, favoritos, pesquisa de arquivos e console cmd.exe embutido.
- Ícones por tipo de arquivo e regras de "abrir com", importação/exportação.
- Ícone na bandeja, inicialização automática, atalho global, menus nativos do Explorador de Arquivos, janela sem bordas redimensionável.
- Janela de boas-vindas exibida uma única vez e verificação de atualizações via GitHub Releases com uma placa no canto.

## Primeira execução e atualizações

- A janela de boas-vindas é exibida uma única vez (nota de beta, escolha do idioma, permissão de atualização, mosaicos de exemplo) e pode ser exibida novamente nas configurações.
- A verificação de atualizações roda a cada N dias (padrão: 3) estritamente com o consentimento do usuário; uma placa verde de "Atualizar" aparece ao lado do botão de configurações quando existe uma versão mais nova; "Verificar agora" é uma verificação manual.

## Doar

Se o Tilettes é útil para você, você pode apoiar o desenvolvimento com cripto. As redes compatíveis com EVM compartilham um mesmo endereço:

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

## Compilar

```
build.bat
```

Requer qualquer Windows com .NET Framework 4.x — o compilador vem incluído no sistema operacional. As versões são criadas automaticamente pelo GitHub Actions a cada tag `v*` e contêm apenas o arquivo de código-fonte (o workflow também verifica a compilação); compile o exe você mesmo com `build.bat`.

Documentação completa: [**README.md**](https://github.com/AlexNoVibe/Tilettes/blob/master/README.md) (English) · [README.ru.md](https://github.com/AlexNoVibe/Tilettes/blob/master/docs/README.ru.md) (Русский)
