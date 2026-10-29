const form = document.getElementById('order-form');
const resultSection = document.getElementById('result');
const resultCard = document.getElementById('result-card');
const historySection = document.getElementById('history');
const historyBody = historySection.querySelector('tbody');
const fixStatus = document.getElementById('fix-status');

const sideLabel = { Buy: 'Compra', Sell: 'Venda' };
const money = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2 });

function el(tag, text, className) {
  const node = document.createElement(tag);
  if (text !== undefined) node.textContent = text;
  if (className) node.className = className;
  return node;
}

function showErrors(errors) {
  document.querySelectorAll('.error').forEach(e => (e.textContent = ''));
  for (const [field, messages] of Object.entries(errors ?? {})) {
    const target = document.querySelector(`.error[data-for="${field}"]`);
    if (target) target.textContent = messages.join(' ');
  }
}

function validate(data) {
  const errors = {};
  const quantity = Number(data.quantity);
  const price = Number(data.price);
  if (!Number.isInteger(quantity) || quantity <= 0 || quantity >= 100000)
    errors.quantity = ['Quantidade deve ser um inteiro positivo menor que 100.000.'];
  if (!(price > 0 && price < 1000) || Math.round(price * 100) !== price * 100)
    errors.price = ['Preço deve ser positivo, múltiplo de 0,01 e menor que 1.000.'];
  return errors;
}

function renderResult(ok, status, rows) {
  resultCard.replaceChildren();
  resultCard.append(el('p', status, `status ${ok ? 'ok' : 'bad'}`));
  const dl = el('dl');
  for (const [k, v] of rows) {
    if (v === undefined || v === null) continue;
    dl.append(el('dt', k), el('dd', String(v)));
  }
  resultCard.append(dl);
  resultSection.hidden = false;
}

function addHistory(order, statusText, ok) {
  const tr = el('tr');
  tr.append(
    el('td', order.symbol),
    el('td', sideLabel[order.side]),
    el('td', order.quantity),
    el('td', money.format(order.price)),
  );
  const td = el('td');
  td.append(el('span', statusText, `status ${ok ? 'ok' : 'bad'}`));
  tr.append(td);
  historyBody.prepend(tr);
  historySection.hidden = false;
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  const raw = Object.fromEntries(new FormData(form));
  const errors = validate(raw);
  showErrors(errors);
  if (Object.keys(errors).length > 0) return;

  const order = {
    symbol: raw.symbol,
    side: raw.side,
    quantity: Number(raw.quantity),
    price: Number(raw.price),
  };

  const button = form.querySelector('button');
  button.disabled = true;
  try {
    const response = await fetch('/api/orders', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(order),
    });
    const body = await response.json().catch(() => ({}));

    if (response.ok) {
      const accepted = body.execType === 'New';
      const statusText = accepted ? 'Aceita (New)' : 'Rejeitada (Rejected)';
      renderResult(accepted, statusText, [
        ['ExecType', body.execType],
        ['ClOrdID', body.clOrdId],
        ['OrderID', body.orderId],
        ['Símbolo', body.symbol],
        ['Lado', sideLabel[body.side]],
        ['Quantidade', body.quantity],
        ['Preço', body.price != null ? money.format(body.price) : null],
        ['Mensagem', body.text],
      ]);
      addHistory(order, statusText, accepted);
    } else {
      showErrors(body.errors);
      renderResult(false, `Erro HTTP ${response.status}`, [
        ['Detalhe', body.detail ?? body.title],
      ]);
    }
  } catch (err) {
    renderResult(false, 'Falha de rede', [['Detalhe', err.message]]);
  } finally {
    button.disabled = false;
  }
});

async function refreshStatus() {
  try {
    const response = await fetch('/api/status');
    const { fixConnected } = await response.json();
    fixStatus.textContent = fixConnected ? 'FIX conectado' : 'FIX desconectado';
    fixStatus.className = `badge ${fixConnected ? 'ok' : 'bad'}`;
  } catch {
    fixStatus.textContent = 'servidor indisponível';
    fixStatus.className = 'badge bad';
  }
}

refreshStatus();
setInterval(refreshStatus, 3000);
