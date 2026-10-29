# Desafio Flowa – Gerador e Acumulador de Ordens

Um sistema simples com duas aplicações: uma página web onde você cria ordens de compra e venda de ações, e um serviço que recebe essas ordens e decide se aceita ou recusa cada uma, de acordo com um limite de exposição por ação.

> This is a challenge by [Coodesh](https://coodesh.com/)

## Tecnologias usadas

- C# com .NET 10
- QuickFIX/n (protocolo FIX 4.4, usado na conversa entre as duas aplicações)
- ASP.NET Core
- HTML, CSS e JavaScript
- xUnit para os testes

## Como funciona

**OrderGenerator** é a parte que você vê. Ele abre uma página com um formulário onde você escolhe:

- a ação (PETR4, VALE3 ou VIIA4);
- se é compra ou venda;
- a quantidade (número inteiro, até 99.999);
- o preço (com no máximo duas casas decimais, até 999,99).

Ao enviar, a ordem vai para o OrderAccumulator e a resposta aparece logo abaixo do formulário, junto com um histórico das ordens enviadas.

**OrderAccumulator** é quem recebe as ordens e faz a conta. Para cada ação ele guarda a exposição:

- compras somam (preço × quantidade);
- vendas subtraem (preço × quantidade).

O limite é de R$ 100 milhões por ação, para mais ou para menos. Se a ordem fizer a exposição passar disso, ela é recusada e não entra na conta. Caso contrário, é aceita e a exposição é atualizada.

Algumas observações:

- Uma ordem que deixa a exposição exatamente em R$ 100 milhões é aceita, só passar disso é recusado.
- Os valores ficam em memória: se o OrderAccumulator for reiniciado, a exposição volta a zero.
- Os campos são conferidos tanto na página quanto nos dois serviços.

## Como rodar

Você precisa do [.NET 10](https://dotnet.microsoft.com/download) instalado.

Abra dois terminais na pasta do projeto. No primeiro, suba o acumulador:

```bash
dotnet run --project src/OrderAccumulator
```

No segundo, suba o gerador:

```bash
dotnet run --project src/OrderGenerator --launch-profile http
```

Depois é só abrir **http://localhost:5000** no navegador. Quando os dois estiverem conectados, aparece "FIX conectado" no canto da página. Se você subir o gerador primeiro, ele conecta sozinho assim que o acumulador estiver no ar.

## Testes

```bash
dotnet test
```

Os testes cobrem as regras do formulário, o cálculo e o limite da exposição e também o caminho completo: enviar uma ordem pela página, passar pelo acumulador e receber a resposta.

## Organização das pastas

```
src/OrderGenerator      página web e envio das ordens
src/OrderAccumulator    recebimento das ordens e controle da exposição
src/FixOrderChallenge.Common   regras e configurações usadas pelos dois
tests/                  testes automatizados
```
