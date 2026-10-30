const E = require('./econ.js');
function run(F, att, withOrders) {
  const S = E.newState(); S.cash = 10; let t = 0; const g = E.globals({}); const unl = [0]; let order = null, done = 0, nextAt = 0;
  while (t < 3600 && unl.length < 5) {
    const inc = E.totalIncome(S, g); S.cash += inc * att; t += att;
    if (withOrders) {
      if (!order && t >= nextAt) { // accepte l'offre du dernier poste ouvert
        let i = 0; S.st.forEach((s, k) => { if (s.u) i = k; });
        const rate = E.itemsPerSec(i, S.st[i], g); let qty = Math.max(3, Math.round(rate * 100));
        order = { i, qty, p: 0, reward: qty * E.moneyPerItem(i, S.st[i], g) * F }; nextAt = t + 8;
      }
      if (order) { order.p += E.itemsPerSec(order.i, S.st[order.i], g) * att; if (order.p >= order.qty) { S.cash += order.reward; done++; order = null; } }
    }
    for (let guard = 0; guard < 40; guard++) {
      const opts = E.options(S, g, 1); opts.forEach(o => { if (o.kind === 'unlock') o.payback *= 0.35; }); const b = E.best(opts); if (!b) break; let pick = null;
      if (b.cost <= S.cash) pick = b; else { const wait = (b.cost - S.cash) / Math.max(1e-9, E.totalIncome(S, g)); if (wait > 15) opts.filter(o => o.cost <= S.cash).forEach(o => { if (!pick || o.payback < pick.payback) pick = o; }); }
      if (!pick) break; S.cash -= Math.ceil(pick.cost);
      if (pick.kind === 'unlock') { S.st[pick.i].u = true; unl.push(Math.round(t)); } else S.st[pick.i][pick.axis] += 1;
    }
  }
  return { unl: unl.join('/'), done };
}
console.log('sans commandes', JSON.stringify(run(0, 5, false)));
for (const F of [3.5, 2, 1.5, 1]) console.log('facteur', F, JSON.stringify(run(F, 5, true)));
