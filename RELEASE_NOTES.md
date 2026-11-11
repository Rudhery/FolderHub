# Notas de versão

O que o app mostra na tela de novidades depois de atualizar, e o que vai no corpo
da release no GitHub. Um arquivo só para os dois, porque são o mesmo texto para a
mesma pessoa: quem baixou o instalador.

Está em português porque é texto de interface. O [CHANGELOG](CHANGELOG.md) é o
outro lado — em inglês, com nome de arquivo e de classe, para quem vai mexer no
código.

## Como escrever

Um bloco `##` por versão, com o número igual ao `<Version>` do csproj — é por ele
que o app acha o bloco da versão que está rodando. Dentro, uma seção `###` por
etiqueta, e um item por linha:

```markdown
## X.Y.Z

### Features
- **Título curto** — a frase que explica, no tom de quem usa.

### Fix
- **O que estava errado** — e o que passa a acontecer agora.
```

As etiquetas reconhecidas são **Features** e **Fix**. Qualquer outra aparece com o
próprio nome e um ícone neutro, então inventar uma não quebra nada — só não ganha
ícone próprio.

O travessão entre o título e o texto é o separador que o app usa. Sem ele, a linha
inteira vira título.

<!-- as versões, mais nova primeiro -->

## 1.2.0

### Features
- **Layout novo, com a busca no topo** — Abriu, já digita. Os hubs ficam numa barra lateral com uma cor cada, e os ícones aparecem soltos, sem caixa em volta.
- **Mais usados** — Os quatro apps que você mais abre ficam no alto. A contagem fica só neste PC.
- **Digitou, virou lista** — Os resultados aparecem em lista com o nome do arquivo e o tipo. Tab leva a busca para o próximo hub.
- **Barra de ações** — Embaixo, o app escolhido com Abrir e o menu Ctrl+K: executar como administrador, mostrar na pasta, copiar caminho.
- **Primeira execução guiada** — Três passos em vez de um seletor de pasta seco: escolher a pasta, definir o atalho e ver o hub montado, com os atalhos que ele encontrou.
- **Novidades depois de atualizar** — Esta tela. Aparece uma vez por versão e sai no primeiro clique.
- **O atalho aparece desenhado** — Na primeira execução as teclas da combinação viram teclas na tela, com o nome que está escrito nelas.

### Fix
- **Atalho duplicado depois de atualizar um app** — A ordem não fica mais no nome do arquivo ("01 - Steam"). Quando um app se atualiza e recria o atalho, ele volta para o mesmo lugar, sem cópia. Pastas antigas são convertidas sozinhas, e a duplicata que já existia vai para a Lixeira.
- **Botão colado no vizinho** — O "Voltar" da primeira execução nascia encostado no "Avançar" quando aparecia depois da tela montada.
- **Cantos largos demais** — As telas novas seguiam um raio próprio; passaram a usar o mesmo do hub e da configuração.
- **A tela de novidades não abria** — Um modelo de lista inválido derrubava a janela com uma caixa de erro em vez de mostrar as novidades.

## 1.1.0

### Features
- **Tema claro** — O hub e a configuração agora vêm em claro também. A escolha está em Aparência, e vale na próxima abertura.
- **Densidade dos cards** — Compacto, padrão ou grande. A janela se redimensiona junto, e o nome do atalho continua com duas linhas nas três.
- **Transparência do acrylic** — Quanto do desktop aparece atrás da janela, num controle só — de opaco a translúcido, aplicando enquanto você arrasta.
- **Tela de configuração** — Tudo num lugar: hubs, atalhos, aparência. Nada espera um OK — cada mudança é gravada e aplicada na hora.

### Fix
- **Abas esticadas** — As pílulas das abas viravam elipses. O raio agora é a metade da altura, que é o que o navegador faz sozinho e o WPF não.
- **Barra de rolagem por poucos pixels** — A grade fecha em linha inteira, então não sobra meia fileira encostada no rodapé.
- **O atalho global que não valia** — A configuração agora avisa quando há atalho definido mas ninguém escutando, em vez de ele simplesmente não funcionar.

## 1.0.0

### Features
- **A pasta é a configuração** — Aponte para uma pasta de atalhos e ela vira um hub visual. Nada de cadastro de app nem banco de dados.
- **Atalho global** — `Ctrl+Alt+Space` abre o hub de qualquer lugar, com o app na bandeja.
- **Abas** — Várias pastas num hub só, cada uma carregada quando você abre.
- **Ordenação na própria pasta** — Arrastar um card renomeia os arquivos com prefixo `01 - `, então a ordem vale no Explorer também.
