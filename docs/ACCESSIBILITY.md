# Acessibilidade (Fase 11)

> **Status:** auditoria e correções concluídas nas telas existentes (Fases 1-10). Sem redesenho
> de nenhuma interação — só ajustes de rotulagem/semântica sobre o que já existia.

## O que já vinha "de graça"

Todas as páginas, desde a Fase 1, já usavam controles nativos do WinUI 3 (`Button`, `ComboBox`,
`TextBox`, `ToggleSwitch`, `NavigationView`, `AppBarButton`...) em vez de controles customizados
desenhados à mão — isso já dá, sem esforço extra, suporte a leitor de tela, navegação por
teclado (Tab/Shift+Tab, Enter/Espaço para ativar) e alto contraste do sistema, porque são
exatamente os mesmos controles que qualquer outro aplicativo Windows usa.

Todo `AppBarButton`/`AppBarToggleButton` (barra de comandos do visualizador de documento) já
usava `Label="..."` — que serve tanto de texto visível quanto de nome de acessibilidade
automático. Todo `Button` do aplicativo já tinha `Content` com texto visível (nunca um botão
"só ícone"). Nenhum ajuste foi necessário nesses dois pontos.

## O que foi corrigido nesta fase

- **Rótulos de campo via `Header`, não `TextBlock` solto ao lado:** `ComboBox` de
  Scanner/Origem/Perfil (`ScanPage`), Formato de saída (`BatchConvertPage`) e Idioma do
  documento (`OcrPage`) tinham um `TextBlock` com o rótulo *antes* do controle, sem nenhuma
  ligação formal entre os dois — um leitor de tela focando o `ComboBox` anunciava só "caixa de
  seleção", sem dizer do que se trata. Trocado por `ComboBox.Header="..."`, que o WinUI já liga
  automaticamente ao nome de acessibilidade do controle (mesmo padrão que `ToggleSwitch.Header`
  já usava em outras telas, sem esse problema).
- **Nome de acessibilidade explícito nos 6 cartões de ação do Início:** cada cartão é um `Button`
  cujo conteúdo é um ícone + dois blocos de texto (título e descrição) — o WinUI normalmente
  consegue montar um nome a partir do texto visível dos filhos, mas isso não é garantido em todos
  os casos. Adicionado `AutomationProperties.Name` explícito em cada um, combinando título e
  descrição (ex.: "Digitalizar documento. Digitalize usando um scanner instalado."), e os ícones
  decorativos dentro deles marcados como `AutomationProperties.AccessibilityView="Raw"` (ocultos
  da árvore de acessibilidade — não acrescentam informação, o texto ao lado já diz tudo).
- **Ícones e imagens puramente decorativos ocultados da árvore de acessibilidade**
  (`AccessibilityView="Raw"`): o ícone de "nenhum scanner encontrado" (`ScanPage`), o ícone
  genérico de tela ainda não implementada (`PlaceholderPage`), a logo pequena ao lado do nome
  "SCANOVA" no menu lateral, e a logo grande no topo do Início — em todos os casos, o texto
  adjacente já transmite a mesma informação; sem isso, um leitor de tela anunciaria uma imagem
  "sem nome" antes do texto de verdade.
- **Nome de acessibilidade dinâmico na imagem principal do visualizador**
  (`DocumentViewerPage.MainImage`): passa a usar o título do documento aberto
  (`AutomationProperties.Name="{x:Bind ViewModel.DocumentTitle}"`) em vez de ficar sem nenhum
  nome — não descreve o conteúdo da imagem (impossível de forma genérica), mas ao menos identifica
  qual documento está em foco.

## Limitação conhecida, aceita conscientemente

O recorte manual (seleção de área para cortar, em `DocumentViewerPage`) só funciona por
arrastar o mouse — não há um jeito de fazer o mesmo ajuste inteiramente pelo teclado. Implementar
isso exigiria desenhar uma interação nova do zero (mover/redimensionar uma seleção com as setas
do teclado, com um jeito de confirmar o tamanho), fora do escopo de uma fase de polimento. Fica
registrado aqui como limitação conhecida, não como algo "esquecido".

## Verificação

Como não é possível rodar o aplicativo neste ambiente (WinUI 3 só compila e roda no Windows), as
correções acima foram feitas por revisão de código e não puderam ser confirmadas com um leitor
de tela de verdade (Narrador do Windows) nem com o Accessibility Insights for Windows — essa
verificação fica pendente de uma primeira execução em uma máquina Windows.
