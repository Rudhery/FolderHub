# web

The FolderHub landing page.

```
index.html
assets/
  styles.css     tokens + layout, os mesmos tokens do app
  motion.js      revelar ao rolar e paralaxe
  fonts/         Manrope e JetBrains Mono, subsetadas
  favicon.svg    a marca de três barras
  og.png         imagem de compartilhamento
```

## Rodando

Não há build. Qualquer servidor estático serve:

```powershell
cd web
python -m http.server 8099
```

E abra <http://127.0.0.1:8099>.

## As decisões

**HTML e CSS estáticos, sem framework e sem build.** É uma página só, sem
estado, sem formulário e sem dados. Astro ou Next trariam `node_modules`, uma
etapa de build e uma pipeline para entregar exatamente o mesmo HTML. O que
existe de JavaScript são 60 linhas próprias.

**Fontes servidas daqui, não do Google.** As mesmas do aplicativo, subsetadas
para latim com `fonttools` e convertidas para WOFF2: **64 KB no total**, contra
397 KB dos TTF originais. Além de ficar menor, evita duas resoluções de DNS,
uma requisição bloqueante e o IP de quem visita indo parar num terceiro — o que
seria contraditório numa página que anuncia "sem telemetria".

**Revelar ao rolar com IntersectionObserver, não com CSS.** A primeira versão
usou scroll-driven animations (`animation-timeline: view()`), que são elegantes
mas só existem no Chrome e no Edge. Sem elas o `both` do CSS deixaria cada
seção parada em `opacity: 0` — a página inteira em branco no Firefox e no
Safari. O observer funciona em todo lugar e ainda permite escalonar os cards.

**Sem interceptar a roda do mouse.** Foi tentado o efeito tipo Lenis, de
interpolar a rolagem quadro a quadro, e o resultado foi pior: o trackpad já
manda inércia própria, então a interpolação vira uma segunda suavização e
aparece como atraso. O que realmente travava eram três coisas caras, hoje
removidas:

- `backdrop-filter: blur(28px)` na janela do mockup — atrás dela só existe o
  fundo chapado da página, então o blur não mostrava nada e re-rasterizava a
  cada quadro. Na barra do topo ele fica, porque lá o conteúdo passa por baixo.
- `filter: blur()` nos dois halos, que já são gradientes radiais suaves.
- leitura de `scrollHeight` a cada evento da roda, que forçava reflow.

Com isso a rolagem fica com o comportamento nativo do sistema e sem quadro
perdido — medido percorrendo a página inteira: mediana de 10 ms, pior caso
10,1 ms, zero quadros acima de 20 ms.

## Publicando

`.github/workflows/pages.yml` publica esta pasta no GitHub Pages. Está em
execução manual porque o repositório ainda é privado; quando ele virar público,
habilite Pages em **Settings → Pages** (origem: GitHub Actions) e descomente o
gatilho de `push` no workflow.

## O conteúdo saiu do design

A página segue os arquivos `Folder Hub Landing v2` e `Folder Hub Landing
Mobile` do Claude Design. Três coisas foram ajustadas por serem factualmente
diferentes do aplicativo:

- a versão e o changelog, que no design paravam na v0.3.0
- "sem serviço em segundo plano", que era falso — existe modo residente na
  bandeja. Virou "sem conta, sem telemetria e sem nada saindo para a nuvem"
- os links de repositório e da pílula de versão, que eram `#`

## O que ainda falta

- **Não existe botão de download.** O design não previa nenhum, e uma landing
  cujo objetivo é entregar o instalador provavelmente quer um. Por ora a pílula
  de versão leva para a página de releases.
- Um GIF do aplicativo em uso valeria mais que o mockup estático.
